using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Accounting.Services;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.Orders;
using Sugarista.Application.Orders.Commands.CreateOrderCheckout;
using Sugarista.Application.Orders.Services;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Pos.Commands.UpsertPosOrder;

public enum PosOrderAction
{
    Hold,
    PayCash,
    PayCard
}

public record PosOrderLineRequest(
    Guid MenuItemId,
    string? SizeKey,
    int Quantity,
    IReadOnlyCollection<Guid> ModifierIds,
    string? BaristaNote = null,
    string? PayerKey = null,
    string? PayerLabel = null);

public record UpsertPosOrderCommand(
    Guid? OrderId,
    string? CustomerName,
    string? CustomerPhone,
    string? Note,
    string ServiceType,
    Guid? SalonTableId,
    IReadOnlyCollection<PosOrderLineRequest> Items,
    string Action,
    bool IsSplitBill = false,
    string? SplitBillMode = null,
    int? GuestCount = null,
    IReadOnlyCollection<CheckoutSplitShareRequest>? SplitShares = null,
    Guid? ServedByAdminUserId = null) : IRequest<UpsertPosOrderResultDto>;

public class UpsertPosOrderCommandValidator : AbstractValidator<UpsertPosOrderCommand>
{
    public UpsertPosOrderCommandValidator()
    {
        RuleFor(c => c.Items).NotEmpty().WithMessage("At least one item is required.");
        RuleForEach(c => c.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.MenuItemId).NotEmpty();
            item.RuleFor(i => i.Quantity).GreaterThan(0);
            item.RuleFor(i => i.BaristaNote).MaximumLength(500);
        });
        RuleFor(c => c.ServiceType)
            .Must(value => Enum.TryParse<PosServiceType>(value, true, out _))
            .WithMessage("Service type must be DineIn or TakeAway.");
        RuleFor(c => c.Action)
            .Must(value => Enum.TryParse<PosOrderAction>(value, true, out _))
            .WithMessage("Action must be Hold, PayCash, or PayCard.");
        RuleFor(c => c.Note).MaximumLength(500);
    }
}

public class UpsertPosOrderCommandHandler : IRequestHandler<UpsertPosOrderCommand, UpsertPosOrderResultDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IAccountingService _accountingService;

    public UpsertPosOrderCommandHandler(
        IApplicationDbContext dbContext,
        IAccountingService accountingService)
    {
        _dbContext = dbContext;
        _accountingService = accountingService;
    }

    public async Task<UpsertPosOrderResultDto> Handle(UpsertPosOrderCommand request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<PosOrderAction>(request.Action, true, out var action))
        {
            throw new InvalidOperationException("Invalid POS action.");
        }

        if (!Enum.TryParse<PosServiceType>(request.ServiceType, true, out var serviceType))
        {
            throw new InvalidOperationException("Invalid service type.");
        }

        var splitBillMode = ResolveSplitBillMode(request);
        var isSplitBill = splitBillMode != SplitBillMode.Single;
        var resolvedLines = await ResolveLinesAsync(request.Items, cancellationToken);
        var totalAmount = resolvedLines.Sum(line => line.resolved.UnitPrice * line.resolved.Quantity);

        ValidateSplitBill(request, splitBillMode, totalAmount);

        var tableSessionId = await ResolveTableSessionIdAsync(request.SalonTableId, serviceType, cancellationToken);

        Order order;
        if (request.OrderId.HasValue)
        {
            order = await _dbContext.Orders
                .Include(o => o.Lines)
                .Include(o => o.SplitShares)
                .FirstOrDefaultAsync(o => o.Id == request.OrderId.Value, cancellationToken)
                ?? throw new KeyNotFoundException("Order was not found.");

            if (order.Channel != OrderChannel.Pos)
            {
                throw new InvalidOperationException("Only POS orders can be edited here.");
            }

            if (order.Status is OrderStatus.Cancelled or OrderStatus.Failed or OrderStatus.Paid)
            {
                throw new InvalidOperationException($"Order in status '{order.Status}' cannot be modified.");
            }

            await ReplaceLinesAsync(order, resolvedLines, cancellationToken);
            ReplaceSplitShares(order, request, splitBillMode, isSplitBill);
            order.RecalculateTotal();
        }
        else
        {
            var trackingCode = await OrderTrackingCodeGenerator.GenerateAsync(_dbContext, cancellationToken);
            order = Order.CreatePosDraft(
                trackingCode,
                request.CustomerName,
                request.CustomerPhone,
                request.Note,
                totalAmount,
                serviceType,
                request.SalonTableId,
                tableSessionId,
                request.ServedByAdminUserId,
                splitBillMode,
                request.GuestCount);

            foreach (var (resolved, lineRequest) in resolvedLines)
            {
                order.AddLine(CreateLine(order.Id, resolved, lineRequest));
            }

            if (isSplitBill && request.SplitShares is { Count: > 0 })
            {
                AddSplitShares(order, request.SplitShares);
            }

            _dbContext.Orders.Add(order);
        }

        order.UpdateCustomerForPos(request.CustomerName, request.CustomerPhone, request.Note);
        order.ConfigurePos(
            OrderChannel.Pos,
            serviceType,
            request.SalonTableId,
            tableSessionId,
            request.ServedByAdminUserId);

        switch (action)
        {
            case PosOrderAction.Hold:
                order.Hold();
                break;
            case PosOrderAction.PayCash:
                order.PayAtPos(PosPaymentMethod.Cash);
                await OrderInventoryService.DeductInventoryForOrderLinesAsync(
                    _dbContext,
                    order,
                    order.Lines.ToList(),
                    cancellationToken);
                break;
            case PosOrderAction.PayCard:
                order.PayAtPos(PosPaymentMethod.Card);
                await OrderInventoryService.DeductInventoryForOrderLinesAsync(
                    _dbContext,
                    order,
                    order.Lines.ToList(),
                    cancellationToken);
                break;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (action is PosOrderAction.PayCash or PosOrderAction.PayCard)
        {
            await _accountingService.PostOrderPaymentAsync(order, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return new UpsertPosOrderResultDto(
            order.Id,
            order.TrackingCode,
            order.Status.ToString(),
            order.TotalAmount,
            order.PaymentMethod?.ToString());
    }

    private async Task ReplaceLinesAsync(
        Order order,
        IReadOnlyCollection<(OrderPricing.ResolvedLine resolved, PosOrderLineRequest request)> resolvedLines,
        CancellationToken cancellationToken)
    {
        foreach (var existingLine in order.Lines.ToList())
        {
            if (existingLine.InventoryDeductedQuantity > 0)
            {
                await OrderInventoryService.RestoreInventoryForOrderLinesAsync(
                    _dbContext,
                    order,
                    [existingLine],
                    cancellationToken);
            }

            _dbContext.OrderLines.Remove(existingLine);
        }

        order.Lines.Clear();

        foreach (var (resolved, lineRequest) in resolvedLines)
        {
            var line = CreateLine(order.Id, resolved, lineRequest);
            order.AddLine(line);
            _dbContext.OrderLines.Add(line);
        }
    }

    private static OrderLine CreateLine(
        Guid orderId,
        OrderPricing.ResolvedLine resolved,
        PosOrderLineRequest lineRequest) =>
        new(
            orderId,
            resolved.MenuItemId,
            resolved.PersianName,
            resolved.EnglishName,
            resolved.SizeKey,
            resolved.SizeLabel,
            resolved.Quantity,
            resolved.UnitPrice,
            resolved.ModifierLabels,
            lineRequest.PayerKey,
            lineRequest.PayerLabel,
            lineRequest.BaristaNote);

    private async Task<IReadOnlyCollection<(OrderPricing.ResolvedLine resolved, PosOrderLineRequest request)>> ResolveLinesAsync(
        IReadOnlyCollection<PosOrderLineRequest> items,
        CancellationToken cancellationToken)
    {
        var menuItemIds = items.Select(item => item.MenuItemId).Distinct().ToList();
        var modifierIds = items.SelectMany(item => item.ModifierIds).Distinct().ToList();

        var menuItems = await _dbContext.MenuItems
            .Where(item => menuItemIds.Contains(item.Id) && item.IsActive)
            .ToListAsync(cancellationToken);

        if (menuItems.Count != menuItemIds.Count)
        {
            throw new KeyNotFoundException("One or more menu items were not found or are inactive.");
        }

        var modifiers = modifierIds.Count == 0
            ? []
            : await _dbContext.MenuModifiers
                .Where(modifier => modifierIds.Contains(modifier.Id) && modifier.IsActive)
                .ToListAsync(cancellationToken);

        var modifiersById = modifiers.ToDictionary(modifier => modifier.Id);

        return items
            .Select(item =>
            {
                var menuItem = menuItems.First(entry => entry.Id == item.MenuItemId);
                var resolved = OrderPricing.ResolveLine(
                    menuItem,
                    item.SizeKey,
                    item.Quantity,
                    modifiersById,
                    item.ModifierIds);
                return (resolved, item);
            })
            .ToArray();
    }

    private async Task<Guid?> ResolveTableSessionIdAsync(
        Guid? salonTableId,
        PosServiceType serviceType,
        CancellationToken cancellationToken)
    {
        if (serviceType != PosServiceType.DineIn || !salonTableId.HasValue)
        {
            return null;
        }

        var session = await _dbContext.TableSessions
            .AsNoTracking()
            .Where(entry => entry.TableId == salonTableId.Value && entry.EndedAt == null)
            .OrderByDescending(entry => entry.StartedAt)
            .Select(entry => new { entry.Id })
            .FirstOrDefaultAsync(cancellationToken);

        return session?.Id;
    }

    private static void ValidateSplitBill(
        UpsertPosOrderCommand request,
        SplitBillMode splitBillMode,
        decimal totalAmount)
    {
        if (splitBillMode == SplitBillMode.ByCustomAmount)
        {
            var shareTotal = request.SplitShares?.Sum(share => share.ShareAmount ?? 0) ?? 0;
            if (shareTotal != totalAmount)
            {
                throw new InvalidOperationException("Custom share amounts must sum to the order total.");
            }
        }

        if (splitBillMode == SplitBillMode.ByPercentage)
        {
            var percentageTotal = request.SplitShares?.Sum(share => share.SharePercentage ?? 0) ?? 0;
            if (percentageTotal != 100)
            {
                throw new InvalidOperationException("Share percentages must sum to 100.");
            }
        }
    }

    private static SplitBillMode ResolveSplitBillMode(UpsertPosOrderCommand command)
    {
        if (!string.IsNullOrWhiteSpace(command.SplitBillMode) &&
            Enum.TryParse<SplitBillMode>(command.SplitBillMode, true, out var parsedMode))
        {
            return parsedMode;
        }

        return command.IsSplitBill ? SplitBillMode.ByItem : SplitBillMode.Single;
    }

    private void ReplaceSplitShares(
        Order order,
        UpsertPosOrderCommand request,
        SplitBillMode splitBillMode,
        bool isSplitBill)
    {
        foreach (var share in order.SplitShares.ToList())
        {
            _dbContext.OrderSplitShares.Remove(share);
        }

        order.SplitShares.Clear();
        order.ConfigureSplitBill(splitBillMode, request.GuestCount);

        if (isSplitBill && request.SplitShares is { Count: > 0 })
        {
            AddSplitShares(order, request.SplitShares);
        }
    }

    private static void AddSplitShares(
        Order order,
        IReadOnlyCollection<CheckoutSplitShareRequest> splitShares)
    {
        var sortOrder = 0;
        foreach (var share in splitShares)
        {
            order.AddSplitShare(new OrderSplitShare(
                order.Id,
                share.PayerKey,
                share.PayerLabel,
                share.SharePercentage,
                share.ShareAmount,
                sortOrder++));
        }
    }
}
