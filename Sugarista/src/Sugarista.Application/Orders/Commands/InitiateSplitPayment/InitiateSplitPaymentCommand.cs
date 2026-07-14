using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.Common.Options;
using Sugarista.Application.Orders.Services;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Orders.Commands.InitiateSplitPayment;

public record InitiateSplitPaymentCommand(Guid OrderId, string PayerKey) : IRequest<InitiateSplitPaymentResultDto>;

public record InitiateSplitPaymentResultDto(
    Guid OrderId,
    Guid PaymentId,
    string PayerKey,
    string PayerLabel,
    decimal Amount,
    string RedirectUrl,
    string PaymentAuthority);

public class InitiateSplitPaymentCommandValidator : AbstractValidator<InitiateSplitPaymentCommand>
{
    public InitiateSplitPaymentCommandValidator()
    {
        RuleFor(c => c.OrderId).NotEmpty();
        RuleFor(c => c.PayerKey).NotEmpty();
    }
}

public class InitiateSplitPaymentCommandHandler : IRequestHandler<InitiateSplitPaymentCommand, InitiateSplitPaymentResultDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IPaymentGateway _paymentGateway;
    private readonly PaymentOptions _paymentOptions;

    public InitiateSplitPaymentCommandHandler(
        IApplicationDbContext dbContext,
        IPaymentGateway paymentGateway,
        IOptions<PaymentOptions> paymentOptions)
    {
        _dbContext = dbContext;
        _paymentGateway = paymentGateway;
        _paymentOptions = paymentOptions.Value;
    }

    public async Task<InitiateSplitPaymentResultDto> Handle(InitiateSplitPaymentCommand request, CancellationToken cancellationToken)
    {
        var payerKey = request.PayerKey.Trim().ToLowerInvariant();

        var order = await _dbContext.Orders
            .Include(o => o.Lines)
            .Include(o => o.Payments)
            .Include(o => o.SplitShares)
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken)
            ?? throw new KeyNotFoundException("Order was not found.");

        if (!order.IsSplitBill)
        {
            throw new InvalidOperationException("This order is not a split bill.");
        }

        if (order.Status is OrderStatus.Cancelled or OrderStatus.Failed or OrderStatus.Paid)
        {
            throw new InvalidOperationException($"Cannot pay for order in status '{order.Status}'.");
        }

        var amount = SplitBillAmountCalculator.ResolvePayerAmount(order, payerKey);
        if (amount <= 0)
        {
            throw new KeyNotFoundException("No amount is assigned to this guest.");
        }

        if (order.Payments.Any(payment =>
                payment.PayerKey == payerKey &&
                payment.Status == OrderPaymentStatus.Paid))
        {
            throw new InvalidOperationException("This guest has already paid.");
        }

        var pendingPayment = order.Payments.FirstOrDefault(payment =>
            payment.PayerKey == payerKey &&
            payment.Status == OrderPaymentStatus.PendingPayment);

        if (pendingPayment is not null)
        {
            var existingRedirect = BuildRedirectUrl(order.Id, pendingPayment.Id, pendingPayment.PaymentAuthority);
            return new InitiateSplitPaymentResultDto(
                order.Id,
                pendingPayment.Id,
                pendingPayment.PayerKey,
                pendingPayment.PayerLabel,
                pendingPayment.Amount,
                existingRedirect,
                pendingPayment.PaymentAuthority);
        }

        var payerLabel = order.SplitShares
                .FirstOrDefault(share => string.Equals(share.PayerKey, payerKey, StringComparison.OrdinalIgnoreCase))
                ?.PayerLabel
            ?? order.Lines
                .FirstOrDefault(line => string.Equals(line.PayerKey, payerKey, StringComparison.OrdinalIgnoreCase))
                ?.PayerLabel
            ?? payerKey;

        var payment = new OrderPayment(order.Id, payerKey, payerLabel, amount, Guid.NewGuid().ToString("N"));

        _dbContext.OrderPayments.Add(payment);
        order.AddPayment(payment);

        var callbackUrl = BuildRedirectUrl(order.Id, payment.Id, authority: null);
        var gatewayPayment = await _paymentGateway.RequestPaymentAsync(
            order.Id,
            amount,
            callbackUrl,
            $"Split bill {order.TrackingCode} - {payerLabel}",
            cancellationToken);

        payment.UpdatePaymentAuthority(gatewayPayment.Authority);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new InitiateSplitPaymentResultDto(
            order.Id,
            payment.Id,
            payment.PayerKey,
            payment.PayerLabel,
            payment.Amount,
            gatewayPayment.RedirectUrl,
            gatewayPayment.Authority);
    }

    private string BuildRedirectUrl(Guid orderId, Guid paymentId, string? authority)
    {
        var baseUrl = _paymentOptions.PublicBaseUrl.TrimEnd('/');
        var url = $"{baseUrl}/?split={orderId}&payment={paymentId}";
        return string.IsNullOrWhiteSpace(authority) ? url : $"{url}&authority={authority}";
    }
}
