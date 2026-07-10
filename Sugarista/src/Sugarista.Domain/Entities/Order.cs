using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

public class Order : BaseAuditableEntity
{
    public Order()
    {
    }

    public Order(
        string trackingCode,
        string customerName,
        string customerPhone,
        string? note,
        int totalAmount,
        string paymentAuthority)
    {
        UpdateCustomer(customerName, customerPhone, note);
        SetTrackingCode(trackingCode);
        SetTotalAmount(totalAmount);
        SetPaymentAuthority(paymentAuthority);
        Status = OrderStatus.PendingPayment;
    }

    public string TrackingCode { get; private set; } = string.Empty;

    public string CustomerName { get; private set; } = string.Empty;

    public string CustomerPhone { get; private set; } = string.Empty;

    public string? Note { get; private set; }

    public OrderStatus Status { get; private set; } = OrderStatus.PendingPayment;

    public int TotalAmount { get; private set; }

    public string PaymentAuthority { get; private set; } = string.Empty;

    public DateTimeOffset? PaidAt { get; private set; }

    public ICollection<OrderLine> Lines { get; private set; } = [];

    public void AddLine(OrderLine line)
    {
        Lines.Add(line);
    }

    public void MarkPaid()
    {
        if (Status != OrderStatus.PendingPayment)
        {
            throw new DomainException("Only pending orders can be marked as paid.");
        }

        Status = OrderStatus.Paid;
        PaidAt = DateTimeOffset.UtcNow;
    }

    public void MarkFailed()
    {
        if (Status == OrderStatus.Paid)
        {
            throw new DomainException("Paid orders cannot be marked as failed.");
        }

        Status = OrderStatus.Failed;
    }

    public void MarkCancelled()
    {
        if (Status == OrderStatus.Paid)
        {
            throw new DomainException("Paid orders cannot be cancelled.");
        }

        Status = OrderStatus.Cancelled;
    }

    public void UpdatePaymentAuthority(string paymentAuthority)
    {
        if (Status != OrderStatus.PendingPayment)
        {
            throw new DomainException("Payment authority can only be updated for pending orders.");
        }

        SetPaymentAuthority(paymentAuthority);
    }

    private void SetTrackingCode(string trackingCode)
    {
        if (string.IsNullOrWhiteSpace(trackingCode))
        {
            throw new DomainException("Order tracking code is required.");
        }

        TrackingCode = trackingCode.Trim();
    }

    private void UpdateCustomer(string customerName, string customerPhone, string? note)
    {
        if (string.IsNullOrWhiteSpace(customerName))
        {
            throw new DomainException("Customer name is required.");
        }

        if (string.IsNullOrWhiteSpace(customerPhone))
        {
            throw new DomainException("Customer phone is required.");
        }

        CustomerName = customerName.Trim();
        CustomerPhone = customerPhone.Trim();
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }

    private void SetTotalAmount(int totalAmount)
    {
        if (totalAmount <= 0)
        {
            throw new DomainException("Order total must be greater than zero.");
        }

        TotalAmount = totalAmount;
    }

    private void SetPaymentAuthority(string paymentAuthority)
    {
        if (string.IsNullOrWhiteSpace(paymentAuthority))
        {
            throw new DomainException("Payment authority is required.");
        }

        PaymentAuthority = paymentAuthority.Trim();
    }
}
