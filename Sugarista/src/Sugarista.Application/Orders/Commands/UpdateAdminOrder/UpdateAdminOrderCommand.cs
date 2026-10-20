using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Customers.Services;
using Sugarista.Application.Orders;
using Sugarista.Application.Orders.Services;
using Sugarista.Domain.Entities;

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
    IReadOnlyCollection<AdminOrderLineRequest>? Lines) : IRequest<AdminOrderDetailDto>;

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
    decimal DeliveryFee,
    decimal TaxTotal,
    decimal ItemsTotal,
    IReadOnlyCollection<AdminOrderLineDto> Lines,
    IReadOnlyCollection<AdminOrderPaymentDto> Payments);

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
    decimal TaxPercent);

public record AdminOrderPaymentDto(
    Guid Id,
    string PayerKey,
    string PayerLabel,
    decimal Amount,
    string Status,
    DateTimeOffset? PaidAt);

public class UpdateAdminOrderCommandValidator : AbstractValidator<UpdateAdminOrderCommand>
{
    public UpdateAdminOrderCommandValidator()
    {
        RuleFor(c => c.OrderId).NotEmpty();
        RuleFor(c => c.CustomerName).MaximumLength(200);
        RuleFor(c => c.CustomerPhone).MaximumLength(30);
        RuleFor(c => c.Note).MaximumLength(500);
    }
}

public class UpdateAdminOrderCommandHandler : IRequestHandler<UpdateAdminOrderCommand, AdminOrderDetailDto>
{
    private readonly IApplicationDbContext _dbContext;

    public UpdateAdminOrderCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AdminOrderDetailDto> Handle(UpdateAdminOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await _dbContext.Orders
            .Include(o => o.Lines)
            .Include(o => o.Payments)
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken)
            ?? throw new KeyNotFoundException("Order was not found.");

        if (order.Status == OrderStatus.Cancelled)
        {
            throw new InvalidOperationException("Cancelled orders cannot be modified.");
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
                order.MarkPaid();
            }
            else
            {
                order.ReconcilePaymentStatus();
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return AdminOrderMapper.ToDetail(order);
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
                existingLine.UpdateUnitPrice(resolved.UnitPrice);
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

internal static class AdminOrderMapper
{
    internal static AdminOrderDetailDto ToDetail(Order order)
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
            order.DeliveryFee,
            order.TaxTotal,
            order.TotalAmount - order.DeliveryFee - order.TaxTotal,
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
                    line.TaxPercent))
                .ToArray(),
            order.Payments
                .Select(payment => new AdminOrderPaymentDto(
                    payment.Id,
                    payment.PayerKey,
                    payment.PayerLabel,
                    payment.Amount,
                    payment.Status.ToString(),
                    payment.PaidAt))
                .ToArray());
    }
}
