using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.Customers.Services;
using Sugarista.Application.DrinkBuilder;
using Sugarista.Application.MenuItems.Bundles;
using Sugarista.Application.Orders;
using Sugarista.Application.Orders.Services;
using Sugarista.Domain.Constants;
using Sugarista.Domain.Entities;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Application.Orders.Commands.UpdateAdminOrder;

public record AdminOrderLineRequest(
    Guid? Id,
    Guid MenuItemId,
    string? SizeKey,
    int Quantity,
    IReadOnlyCollection<Guid> ModifierIds,
    string? PayerKey,
    string? PayerLabel,
    bool Remove = false);

public record UpdateAdminOrderCommand(
    Guid OrderId,
    string? CustomerName,
    string? CustomerPhone,
    string? Note,
    IReadOnlyCollection<AdminOrderLineRequest>? Lines,
    string? ManagerNote = null) : IRequest<AdminOrderDetailDto>;

public record AdminOrderDetailDto(
    Guid Id,
    string TrackingCode,
    string CustomerName,
    string CustomerPhone,
    string? Note,
    string Status,
    string FulfillmentStatus,
    decimal TotalAmount,
    decimal AmountPaid,
    bool IsSplitBill,
    int? GuestCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PaidAt,
    DateTimeOffset? FulfillmentUpdatedAt,
    DateTimeOffset? ReadyAt,
    DateTimeOffset? ReadySmsSentAt,
    DateTimeOffset? DeliveryAt,
    decimal DeliveryFee,
    decimal TaxTotal,
    decimal ItemsTotal,
    IReadOnlyCollection<AdminOrderLineDto> Lines,
    IReadOnlyCollection<AdminOrderPaymentDto> Payments,
    string OrderKind = "Standard",
    string? ComplimentaryReason = null,
    bool IsExcludedFromAccounting = false,
    DateTimeOffset? ExcludedFromAccountingAt = null,
    string? ExcludedFromAccountingReason = null,
    string? PaymentMethod = null,
    decimal DiscountTotal = 0m,
    string? ManualDiscountType = null,
    decimal? ManualDiscountValue = null,
    decimal ManualDiscountAmount = 0m,
    string? ManualDiscountReason = null,
    string? ManualDiscountAppliedBy = null,
    IReadOnlyCollection<AdminOrderDiscountDto>? Discounts = null,
    string? ManagerNote = null,
    Guid? CustomerId = null);

public record AdminOrderDiscountDto(
    Guid Id,
    string Kind,
    string Name,
    decimal Amount,
    string? Code,
    DateTimeOffset RedeemedAt);

public record AdminOrderLineDto(
    Guid Id,
    Guid MenuItemId,
    string PersianName,
    string EnglishName,
    string? SizeKey,
    string? SizeLabel,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal,
    string? PayerKey,
    string? PayerLabel,
    int InventoryDeductedQuantity,
    IReadOnlyCollection<string> ModifierLabels,
    decimal TaxPercent,
    IReadOnlyCollection<string> CustomDrinkSummary,
    DrinkBuilderSnapshot? CustomDrink,
    IReadOnlyCollection<string> OptionLabels,
    OrderLineBundleSnapshot? Bundle = null);

public record AdminOrderPaymentDto(
    Guid Id,
    string PayerKey,
    string PayerLabel,
    decimal Amount,
    string Status,
    DateTimeOffset? PaidAt,
    string? PaymentMethod = null,
    string? ReferenceId = null);

public class UpdateAdminOrderCommandValidator : AbstractValidator<UpdateAdminOrderCommand>
{
    public UpdateAdminOrderCommandValidator()
    {
        RuleFor(c => c.OrderId).NotEmpty();
        RuleFor(c => c.CustomerName).MaximumLength(200);
        RuleFor(c => c.CustomerPhone).MaximumLength(30);
        RuleFor(c => c.Note).MaximumLength(500);
        RuleFor(c => c.ManagerNote).MaximumLength(1000);
    }
}

public class UpdateAdminOrderCommandHandler : IRequestHandler<UpdateAdminOrderCommand, AdminOrderDetailDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public UpdateAdminOrderCommandHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<AdminOrderDetailDto> Handle(UpdateAdminOrderCommand request, CancellationToken cancellationToken)
    {
        var canManageManagerNote = _currentUserService.HasPermission(AdminPermissions.Pos.ManagerNote);
        if (!canManageManagerNote && !string.IsNullOrWhiteSpace(request.ManagerNote))
        {
            throw new DomainException("شما مجوز ثبت یا ویرایش یادداشت مدیریت را ندارید.");
        }

        var order = await _dbContext.Orders
            .Include(o => o.Lines)
            .Include(o => o.Payments)
            .Include(o => o.Discounts)
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken)
            ?? throw new KeyNotFoundException("Order was not found.");

        if (order.Status == OrderStatus.Cancelled)
        {
            throw new InvalidOperationException("Cancelled orders cannot be modified.");
        }

        if (canManageManagerNote && request.ManagerNote is not null)
        {
            order.SetManagerNote(request.ManagerNote);
        }

        if (!string.IsNullOrWhiteSpace(request.CustomerName) &&
            !string.IsNullOrWhiteSpace(request.CustomerPhone))
        {
            order.UpdateCustomer(request.CustomerName, request.CustomerPhone, request.Note ?? order.Note);

            var customer = await CustomerDirectoryService.ResolveCustomerAsync(
                _dbContext,
                request.CustomerName,
                request.CustomerPhone,
                "admin",
                cancellationToken);
            order.AssignCustomer(customer?.Id);
        }
        else if (request.Note is not null)
        {
            order.UpdateCustomer(order.CustomerName, order.CustomerPhone, request.Note);
        }

        if (request.Lines is not null)
        {
            await ApplyLineChangesAsync(order, request.Lines, cancellationToken);
            order.RecalculateTotal();

            if (order.AmountPaid > order.TotalAmount)
            {
                throw new InvalidOperationException("Order total cannot be less than the amount already paid.");
            }

            if (order.AmountPaid >= order.TotalAmount && order.TotalAmount > 0)
            {
                if (order.Status is OrderStatus.PendingPayment or OrderStatus.PartiallyPaid)
                {
                    order.MarkPaid();
                }
                else if (order.Status == OrderStatus.Paid)
                {
                    order.RegisterForFulfillment();
                }
            }
            else if (order.Status != OrderStatus.Held)
            {
                order.ReconcilePaymentStatus();
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return AdminOrderMapper.ToDetail(order, canManageManagerNote);
    }

    private async Task ApplyLineChangesAsync(
        Order order,
        IReadOnlyCollection<AdminOrderLineRequest> lineRequests,
        CancellationToken cancellationToken)
    {
        var existingLines = order.Lines.ToDictionary(line => line.Id);
        var menuItemIds = lineRequests
            .Where(line => !line.Remove)
            .Select(line => line.MenuItemId)
            .Distinct()
            .ToList();

        var menuItems = await _dbContext.MenuItems
            .Where(item => menuItemIds.Contains(item.Id))
            .ToListAsync(cancellationToken);

        var modifierIds = lineRequests
            .Where(line => !line.Remove)
            .SelectMany(line => line.ModifierIds)
            .Distinct()
            .ToList();

        var modifiers = modifierIds.Count == 0
            ? []
            : await _dbContext.MenuModifiers
                .Where(modifier => modifierIds.Contains(modifier.Id))
                .ToListAsync(cancellationToken);

        var modifiersById = modifiers.ToDictionary(modifier => modifier.Id);

        foreach (var lineRequest in lineRequests)
        {
            if (lineRequest.Id.HasValue && existingLines.TryGetValue(lineRequest.Id.Value, out var existingLine))
            {
                if (lineRequest.Remove)
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
                    continue;
                }

                var previousQuantity = existingLine.Quantity;
                var menuItem = menuItems.First(item => item.Id == lineRequest.MenuItemId);
                var resolved = OrderPricing.ResolveLine(
                    menuItem,
                    lineRequest.SizeKey,
                    lineRequest.Quantity,
                    modifiersById,
                    lineRequest.ModifierIds);

                existingLine.UpdateQuantity(resolved.Quantity);
                existingLine.UpdateUnitPrice(DrinkBuilderPricing.ResolveEditedUnitPrice(
                    resolved.UnitPrice,
                    existingLine.CustomizationJson));
                existingLine.AssignPayer(lineRequest.PayerKey, lineRequest.PayerLabel);

                await OrderInventoryService.ApplyLineQuantityChangeAsync(
                    _dbContext,
                    order,
                    existingLine,
                    previousQuantity,
                    cancellationToken);
                continue;
            }

            if (lineRequest.Remove)
            {
                continue;
            }

            var item = menuItems.First(entry => entry.Id == lineRequest.MenuItemId);
            var resolvedLine = OrderPricing.ResolveLine(
                item,
                lineRequest.SizeKey,
                lineRequest.Quantity,
                modifiersById,
                lineRequest.ModifierIds);

            var newLine = new OrderLine(
                order.Id,
                resolvedLine.MenuItemId,
                resolvedLine.PersianName,
                resolvedLine.EnglishName,
                resolvedLine.SizeKey,
                resolvedLine.SizeLabel,
                resolvedLine.Quantity,
                resolvedLine.UnitPrice,
                resolvedLine.ModifierLabels,
                lineRequest.PayerKey,
                lineRequest.PayerLabel);

            order.AddLine(newLine);
            _dbContext.OrderLines.Add(newLine);

            if (order.AmountPaid > 0 || order.Status == OrderStatus.Paid)
            {
                await OrderInventoryService.DeductInventoryForOrderLinesAsync(
                    _dbContext,
                    order,
                    [newLine],
                    cancellationToken);
            }
        }
    }
}

public static class AdminOrderMapper
{
    public static AdminOrderDetailDto ToDetail(Order order, bool includeManagerNote = false)
    {
        return new AdminOrderDetailDto(
            order.Id,
            order.TrackingCode,
            order.CustomerName,
            order.CustomerPhone,
            order.Note,
            order.Status.ToString(),
            order.FulfillmentStatus.ToString(),
            order.TotalAmount,
            order.AmountPaid,
            order.IsSplitBill,
            order.GuestCount,
            order.CreatedAt,
            order.PaidAt,
            order.FulfillmentUpdatedAt,
            order.ReadyAt,
            order.ReadySmsSentAt,
            order.DeliveryAt,
            order.DeliveryFee,
            order.TaxTotal,
            order.Lines.Sum(line => line.LineTotal),
            order.Lines
                .Select(line => new AdminOrderLineDto(
                    line.Id,
                    line.MenuItemId,
                    line.PersianName,
                    line.EnglishName,
                    line.SizeKey,
                    line.SizeLabel,
                    line.Quantity,
                    line.UnitPrice,
                    line.LineTotal,
                    line.PayerKey,
                    line.PayerLabel,
                    line.InventoryDeductedQuantity,
                    line.ModifierLabels,
                    line.TaxPercent,
                    line.CustomDrinkSummary,
                    DrinkBuilderPricing.Deserialize(line.CustomizationJson),
                    line.OptionLabels,
                    BundlePricingService.DeserializeSnapshot(line.BundleSnapshotJson)))
                .ToArray(),
            order.Payments
                .Select(payment => new AdminOrderPaymentDto(
                    payment.Id,
                    payment.PayerKey,
                    payment.PayerLabel,
                    payment.Amount,
                    payment.Status.ToString(),
                    payment.PaidAt,
                    payment.PaymentMethod?.ToString(),
                    payment.ReferenceId))
                .ToArray(),
            order.OrderKind.ToString(),
            order.ComplimentaryReason,
            order.IsExcludedFromAccounting,
            order.ExcludedFromAccountingAt,
            order.ExcludedFromAccountingReason,
            order.PaymentMethod?.ToString(),
            order.DiscountTotal,
            order.ManualDiscountType?.ToString(),
            order.ManualDiscountValue,
            order.ManualDiscountAmount,
            order.ManualDiscountReason,
            order.ManualDiscountAppliedBy,
            order.Discounts
                .OrderBy(discount => discount.RedeemedAt)
                .Select(discount => new AdminOrderDiscountDto(
                    discount.Id,
                    discount.Kind.ToString(),
                    discount.Name,
                    discount.Amount,
                    discount.Code,
                    discount.RedeemedAt))
                .ToArray(),
            includeManagerNote ? order.ManagerNote : null,
            order.CustomerId);
    }
}
