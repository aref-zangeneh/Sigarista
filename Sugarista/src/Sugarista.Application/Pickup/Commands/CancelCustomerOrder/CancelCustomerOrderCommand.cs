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

        // Wallet-funded amounts can be returned automatically. Online gateway
        // payments stay a café-side decision: in CardToCard mode the café
        // settles the refund manually, and even in Wallet mode any amount that
        // was not funded by the wallet still needs café handling.
        var walletPaidAmount = pickupSettings.RefundsToWallet && order.AmountPaid > 0
            ? await _dbContext.WalletTransactions
                .Where(transaction =>
                    transaction.OrderId == order.Id &&
                    transaction.Type == WalletTransactionType.Purchase)
                .SumAsync(transaction => -transaction.Amount, cancellationToken)
            : 0m;

        var refundedToWallet = Math.Max(0m, walletPaidAmount);
        if (refundedToWallet > 0)
        {
            await WalletService.RefundCancelledOrderAsync(_dbContext, order, cancellationToken);
        }

        var refundHandledByCafe = order.AmountPaid - refundedToWallet > 0.01m;

        order.AdminCancel();
        await _loyaltyService.RefundForCancelledOrderAsync(order, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var message = (refundedToWallet > 0, refundHandledByCafe) switch
        {
            (true, true) =>
                "سفارش لغو شد و ظرفیت بازه آزاد شد. بخشی از مبلغ به کیف پول شما برگشت داده شد و بازپرداخت باقی‌مانده توسط کافه انجام می‌شود.",
            (true, false) =>
                "سفارش لغو شد و ظرفیت بازه آزاد شد. مبلغ به کیف پول شما برگشت داده شد.",
            (false, true) when pickupSettings.RefundsToWallet =>
                "سفارش لغو شد و ظرفیت بازه آزاد شد. بازپرداخت مبلغ پرداختی توسط کافه انجام می‌شود.",
            (false, true) =>
                "سفارش لغو شد و ظرفیت بازه آزاد شد. بازپرداخت به‌صورت کارت‌به‌کارت توسط کافه انجام می‌شود.",
            _ => "سفارش لغو شد و ظرفیت بازه آزاد شد."
        };

        return ToResult(order, message, refundHandledByCafe, refundedToWallet, pickupSettings.RefundMode);
    }

    private static CustomerOrderActionResultDto ToResult(
        Order order,
        string message,
        bool refundHandledByCafe = false,
        decimal refundedAmount = 0,
        string refundMode = "Wallet") => new(
        order.TrackingCode,
        order.Status.ToString(),
        order.FulfillmentStatus.ToString(),
        order.DeliveryAt,
        message,
        RefundedAmount: refundedAmount,
        RefundHandledByCafe: refundHandledByCafe,
        RefundMode: refundMode);
}
