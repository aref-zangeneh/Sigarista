using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.Orders.Services;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Orders.Commands.ConfirmSplitMockPayment;

public record ConfirmSplitMockPaymentCommand(Guid OrderId, Guid PaymentId, string Authority)
    : IRequest<ConfirmSplitMockPaymentResultDto>;

public record ConfirmSplitMockPaymentResultDto(
    Guid OrderId,
    string TrackingCode,
    string OrderStatus,
    string PayerLabel,
    decimal PaidAmount,
    decimal TotalAmount,
    decimal AmountPaid,
    bool IsFullyPaid);

public class ConfirmSplitMockPaymentCommandValidator : AbstractValidator<ConfirmSplitMockPaymentCommand>
{
    public ConfirmSplitMockPaymentCommandValidator()
    {
        RuleFor(c => c.OrderId).NotEmpty();
        RuleFor(c => c.PaymentId).NotEmpty();
        RuleFor(c => c.Authority).NotEmpty();
    }
}

public class ConfirmSplitMockPaymentCommandHandler
    : IRequestHandler<ConfirmSplitMockPaymentCommand, ConfirmSplitMockPaymentResultDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IPaymentGateway _paymentGateway;

    public ConfirmSplitMockPaymentCommandHandler(
        IApplicationDbContext dbContext,
        IPaymentGateway paymentGateway)
    {
        _dbContext = dbContext;
        _paymentGateway = paymentGateway;
    }

    public async Task<ConfirmSplitMockPaymentResultDto> Handle(
        ConfirmSplitMockPaymentCommand request,
        CancellationToken cancellationToken)
    {
        var order = await _dbContext.Orders
            .Include(o => o.Lines)
            .Include(o => o.Payments)
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken)
            ?? throw new KeyNotFoundException("Order was not found.");

        if (!order.IsSplitBill)
        {
            throw new InvalidOperationException("This order is not a split bill.");
        }

        var payment = order.Payments.FirstOrDefault(entry => entry.Id == request.PaymentId)
            ?? throw new KeyNotFoundException("Payment was not found.");

        if (!string.Equals(payment.PaymentAuthority, request.Authority, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Payment authority does not match.");
        }

        if (payment.Status == OrderPaymentStatus.Paid)
        {
            return BuildResult(order, payment, order.Status == OrderStatus.Paid);
        }

        if (payment.Status != OrderPaymentStatus.PendingPayment)
        {
            throw new InvalidOperationException($"Payment cannot be confirmed while in status '{payment.Status}'.");
        }

        var verification = await _paymentGateway.VerifyPaymentAsync(
            request.Authority,
            payment.Amount,
            cancellationToken);

        if (!verification.IsSuccessful)
        {
            payment.MarkFailed();
            await _dbContext.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException(verification.Message ?? "Payment verification failed.");
        }

        payment.MarkPaid(verification.ReferenceId);

        var payerLines = order.Lines
            .Where(line => string.Equals(line.PayerKey, payment.PayerKey, StringComparison.OrdinalIgnoreCase))
            .ToList();

        await OrderInventoryService.DeductInventoryForOrderLinesAsync(
            _dbContext,
            order,
            payerLines,
            cancellationToken);

        order.RecordPartialPayment(payment.Amount);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return BuildResult(order, payment, order.Status == OrderStatus.Paid);
    }

    private static ConfirmSplitMockPaymentResultDto BuildResult(Order order, OrderPayment payment, bool isFullyPaid)
    {
        return new ConfirmSplitMockPaymentResultDto(
            order.Id,
            order.TrackingCode,
            order.Status.ToString(),
            payment.PayerLabel,
            payment.Amount,
            order.TotalAmount,
            order.AmountPaid,
            isFullyPaid);
    }
}
