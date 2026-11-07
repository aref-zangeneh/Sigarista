using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.Customers.Services;
using Sugarista.Application.DeliveryMethods.Services;
using Sugarista.Application.Orders.Services;
using Sugarista.Domain.Entities;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Application.Pickup.Commands.CancelCustomerOrder;

/// <summary>
/// Customer-initiated cancellation. Allowed only until the café's cancel
/// deadline; frees the slot immediately because capacity is derived from the
/// order's DeliveryAt.
/// </summary>
public record CancelCustomerOrderCommand(Guid OrderId, string TrackingCode)
    : IRequest<CustomerOrderActionResultDto>;

public class CancelCustomerOrderCommandValidator : AbstractValidator<CancelCustomerOrderCommand>
{
    public CancelCustomerOrderCommandValidator()
    {
        RuleFor(command => command.OrderId).NotEmpty();
        RuleFor(command => command.TrackingCode).NotEmpty().MaximumLength(20);
    }
}

public class CancelCustomerOrderCommandHandler
    : IRequestHandler<CancelCustomerOrderCommand, CustomerOrderActionResultDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IDateTime _clock;
    private readonly ILoyaltyService _loyaltyService;

    public CancelCustomerOrderCommandHandler(
        IApplicationDbContext dbContext,
        IDateTime clock,
        ILoyaltyService loyaltyService)
    {
        _dbContext = dbContext;
        _clock = clock;
        _loyaltyService = loyaltyService;
    }

    public async Task<CustomerOrderActionResultDto> Handle(
        CancelCustomerOrderCommand request,
        CancellationToken cancellationToken)
    {
        var trackingCode = request.TrackingCode.Trim();
        var order = await _dbContext.Orders
            .Include(entity => entity.Lines)
            .Include(entity => entity.Payments)
            .FirstOrDefaultAsync(
                entity => entity.Id == request.OrderId && entity.TrackingCode == trackingCode,
                cancellationToken)
            ?? throw new KeyNotFoundException("سفارشی با این کد پیگیری پیدا نشد.");

        if (order.Status == OrderStatus.Cancelled)
        {
            return ToResult(order, "این سفارش قبلاً لغو شده است.");
        }

        var siteSettings = await _dbContext.SiteSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);
        var pickupSettings = PickupSettingsSnapshot.From(siteSettings);
        var now = _clock.UtcNow;

        var blockedReason = PickupScheduleCalculator.BuildChangeBlockedReason(order, pickupSettings, now);
        if (blockedReason is not null)
        {
            throw new DomainException(blockedReason);
        }

        await OrderInventoryService.RestoreInventoryForOrderLinesAsync(
            _dbContext,
            order,
            order.Lines.ToList(),
            cancellationToken);
        await DeliveryMethodInventoryService.RestoreForOrderAsync(_dbContext, order, cancellationToken);

        foreach (var payment in order.Payments.Where(payment => payment.Status == OrderPaymentStatus.PendingPayment))
        {
            payment.MarkCancelled();
        }

        var refundHandledByCafe = !pickupSettings.AutoRefundOnCancel && order.AmountPaid > 0;
        if (pickupSettings.AutoRefundOnCancel && order.AmountPaid > 0)
        {
            // Only wallet-funded amounts can be returned automatically; online
            // gateway refunds stay a café-side decision (documented in TASK-012).
            await WalletService.RefundCancelledOrderAsync(_dbContext, order, cancellationToken);
        }

        order.AdminCancel();
        await _loyaltyService.RefundForCancelledOrderAsync(order, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var message = refundHandledByCafe
            ? "سفارش لغو شد و ظرفیت بازه آزاد شد. بازپرداخت سفارش پرداخت‌شده توسط کافه انجام می‌شود."
            : "سفارش لغو شد و ظرفیت بازه آزاد شد.";

        return ToResult(order, message, refundHandledByCafe);
    }

    private static CustomerOrderActionResultDto ToResult(
        Order order,
        string message,
        bool refundHandledByCafe = false) => new(
        order.TrackingCode,
        order.Status.ToString(),
        order.FulfillmentStatus.ToString(),
        order.DeliveryAt,
        message,
        RefundedAmount: 0,
        RefundHandledByCafe: refundHandledByCafe);
}
