using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

public class OrderPayment : BaseEntity
{
    private OrderPayment()
    {
    }

    public OrderPayment(
        Guid orderId,
        string payerKey,
        string payerLabel,
        decimal amount,
        string paymentAuthority)
    {
        if (orderId == Guid.Empty)
        {
            throw new DomainException("Order payment order is required.");
        }

        if (string.IsNullOrWhiteSpace(payerKey))
        {
            throw new DomainException("Payer key is required.");
        }

        if (amount <= 0)
        {
            throw new DomainException("Payment amount must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(paymentAuthority))
        {
            throw new DomainException("Payment authority is required.");
        }

        OrderId = orderId;
        PayerKey = payerKey.Trim().ToLowerInvariant();
        PayerLabel = string.IsNullOrWhiteSpace(payerLabel) ? PayerKey : payerLabel.Trim();
        Amount = amount;
        PaymentAuthority = paymentAuthority.Trim();
        Status = OrderPaymentStatus.PendingPayment;
    }

    public Guid OrderId { get; private set; }

    public Order Order { get; private set; } = null!;

    public string PayerKey { get; private set; } = string.Empty;

    public string PayerLabel { get; private set; } = string.Empty;

    public decimal Amount { get; private set; }

    public string PaymentAuthority { get; private set; } = string.Empty;

    public OrderPaymentStatus Status { get; private set; } = OrderPaymentStatus.PendingPayment;

    public DateTimeOffset? PaidAt { get; private set; }

    public string? ReferenceId { get; private set; }

    public void MarkPaid(string? referenceId = null)
    {
        if (Status != OrderPaymentStatus.PendingPayment)
        {
            throw new DomainException("Only pending payments can be marked as paid.");
        }

        Status = OrderPaymentStatus.Paid;
        PaidAt = DateTimeOffset.UtcNow;
        ReferenceId = string.IsNullOrWhiteSpace(referenceId) ? null : referenceId.Trim();
    }

    public void MarkFailed()
    {
        if (Status == OrderPaymentStatus.Paid)
        {
            throw new DomainException("Paid payments cannot be marked as failed.");
        }

        Status = OrderPaymentStatus.Failed;
    }

    public void MarkCancelled()
    {
        if (Status == OrderPaymentStatus.Paid)
        {
            throw new DomainException("Paid payments cannot be cancelled.");
        }

        Status = OrderPaymentStatus.Cancelled;
    }

    public void UpdatePaymentAuthority(string paymentAuthority)
    {
        if (Status != OrderPaymentStatus.PendingPayment)
        {
            throw new DomainException("Payment authority can only be updated for pending payments.");
        }

        if (string.IsNullOrWhiteSpace(paymentAuthority))
        {
            throw new DomainException("Payment authority is required.");
        }

        PaymentAuthority = paymentAuthority.Trim();
    }
}
