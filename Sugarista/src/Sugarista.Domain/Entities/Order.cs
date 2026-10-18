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
        decimal totalAmount,
        string paymentAuthority,
        SplitBillMode splitBillMode = SplitBillMode.Single,
        int? guestCount = null)
    {
        UpdateCustomer(customerName, customerPhone, note);
        SetTrackingCode(trackingCode);
        SetTotalAmount(totalAmount);
        SetPaymentAuthority(paymentAuthority);
        SplitBillMode = splitBillMode;
        IsSplitBill = splitBillMode != SplitBillMode.Single;
        GuestCount = IsSplitBill ? guestCount : null;
        Status = OrderStatus.PendingPayment;
        AmountPaid = 0;
    }

    public string TrackingCode { get; private set; } = string.Empty;

    public string CustomerName { get; private set; } = string.Empty;

    public string CustomerPhone { get; private set; } = string.Empty;

    public string? Note { get; private set; }

    public OrderStatus Status { get; private set; } = OrderStatus.PendingPayment;

    public decimal TotalAmount { get; private set; }

    public decimal AmountPaid { get; private set; }

    public bool IsSplitBill { get; private set; }

    public SplitBillMode SplitBillMode { get; private set; } = SplitBillMode.Single;

    public int? GuestCount { get; private set; }

    public string PaymentAuthority { get; private set; } = string.Empty;

    public DateTimeOffset? PaidAt { get; private set; }

    public FulfillmentStatus FulfillmentStatus { get; private set; } = FulfillmentStatus.None;

    public DateTimeOffset? FulfillmentUpdatedAt { get; private set; }

    public DateTimeOffset? ReadyAt { get; private set; }

    public DateTimeOffset? ReadySmsSentAt { get; private set; }

    public OrderChannel Channel { get; private set; } = OrderChannel.Online;

    public PosServiceType? ServiceType { get; private set; }

    public PosPaymentMethod? PaymentMethod { get; private set; }

    public Guid? SalonTableId { get; private set; }

    public SalonTable? SalonTable { get; private set; }

    public Guid? TableSessionId { get; private set; }

    public TableSession? TableSession { get; private set; }

    public Guid? ServedByAdminUserId { get; private set; }

    /// <summary>Delivery/service method applied to this order; null = no delivery method fee/packaging set.</summary>
    public Guid? DeliveryMethodId { get; private set; }

    public DeliveryMethod? DeliveryMethod { get; private set; }

    public decimal DeliveryFee { get; private set; }

    /// <summary>True once the delivery method's disposable packaging set has been deducted from stock.</summary>
    public bool PackagingDeducted { get; private set; }

    public DateTimeOffset? HeldAt { get; private set; }

    public ICollection<OrderLine> Lines { get; private set; } = [];

    public ICollection<OrderPayment> Payments { get; private set; } = [];

    public ICollection<OrderSplitShare> SplitShares { get; private set; } = [];

    public void AddLine(OrderLine line) => Lines.Add(line);

    public void AddPayment(OrderPayment payment) => Payments.Add(payment);

    public void AddSplitShare(OrderSplitShare share) => SplitShares.Add(share);

    public void UpdateCustomer(string customerName, string customerPhone, string? note)
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

    public void RecalculateTotal()
    {
        if (Lines.Count == 0)
        {
            throw new DomainException("Order must contain at least one line.");
        }

        TotalAmount = Lines.Sum(line => line.LineTotal) + DeliveryFee;
    }

    /// <summary>
    /// Attach (or clear) the delivery method; the fee becomes part of the order total
    /// (items total + delivery fee). Total is only recomputed when lines exist.
    /// </summary>
    public void ApplyDeliveryMethod(Guid? deliveryMethodId, decimal deliveryFee)
    {
        if (deliveryFee < 0)
        {
            throw new DomainException("Delivery fee cannot be negative.");
        }

        DeliveryMethodId = deliveryMethodId;
        DeliveryFee = deliveryFee;

        if (Lines.Count > 0)
        {
            RecalculateTotal();
        }
    }

    public void SetPackagingDeducted(bool deducted) => PackagingDeducted = deducted;

    public void MarkPaid()
    {
        if (Status is not (OrderStatus.PendingPayment or OrderStatus.PartiallyPaid))
        {
            throw new DomainException("Only pending or partially paid orders can be marked as paid.");
        }

        Status = OrderStatus.Paid;
        AmountPaid = TotalAmount;
        PaidAt = DateTimeOffset.UtcNow;
        RegisterForFulfillment();
    }

    public void RegisterForFulfillment()
    {
        if (Status != OrderStatus.Paid)
        {
            return;
        }

        if (FulfillmentStatus == FulfillmentStatus.None)
        {
            FulfillmentStatus = FulfillmentStatus.Registered;
            FulfillmentUpdatedAt = DateTimeOffset.UtcNow;
        }
    }

    public void UpdateFulfillment(FulfillmentStatus status)
    {
        if (Status != OrderStatus.Paid)
        {
            throw new DomainException("Only paid orders can be updated in the kitchen queue.");
        }

        if (FulfillmentStatus == FulfillmentStatus.None)
        {
            FulfillmentStatus = FulfillmentStatus.Registered;
            FulfillmentUpdatedAt = DateTimeOffset.UtcNow;
        }

        if (FulfillmentStatus == FulfillmentStatus.Completed && status != FulfillmentStatus.Completed)
        {
            throw new DomainException("Completed orders cannot move back in the kitchen queue.");
        }

        FulfillmentStatus = status;
        FulfillmentUpdatedAt = DateTimeOffset.UtcNow;

        if (status == FulfillmentStatus.Ready)
        {
            ReadyAt ??= DateTimeOffset.UtcNow;
        }
    }

    public void MarkReadySmsSent()
    {
        ReadySmsSentAt = DateTimeOffset.UtcNow;
    }

    public void RecordPartialPayment(decimal amount)
    {
        if (amount <= 0)
        {
            throw new DomainException("Payment amount must be greater than zero.");
        }

        if (Status is OrderStatus.Cancelled or OrderStatus.Failed or OrderStatus.Paid)
        {
            throw new DomainException($"Cannot record payment while order is {Status}.");
        }

        AmountPaid += amount;

        if (AmountPaid >= TotalAmount)
        {
            MarkPaid();
            return;
        }

        Status = OrderStatus.PartiallyPaid;
    }

    public void MarkFailed()
    {
        if (Status == OrderStatus.Paid)
        {
            throw new DomainException("Paid orders cannot be marked as failed.");
        }

        if (AmountPaid > 0)
        {
            throw new DomainException("Partially paid orders cannot be marked as failed.");
        }

        Status = OrderStatus.Failed;
    }

    public void MarkCancelled()
    {
        if (Status == OrderStatus.Paid)
        {
            throw new DomainException("Paid orders cannot be cancelled without restoring inventory.");
        }

        Status = OrderStatus.Cancelled;
    }

    public void AdminCancel() => Status = OrderStatus.Cancelled;

    public void UpdatePaymentAuthority(string paymentAuthority)
    {
        if (IsSplitBill)
        {
            throw new DomainException("Split bill orders use per-payer payment authorities.");
        }

        if (Status != OrderStatus.PendingPayment)
        {
            throw new DomainException("Payment authority can only be updated for pending orders.");
        }

        SetPaymentAuthority(paymentAuthority);
    }

    public bool CanBeModifiedByAdmin()
    {
        return Status is OrderStatus.Held or OrderStatus.PendingPayment or OrderStatus.PartiallyPaid or OrderStatus.Paid;
    }

    public void ConfigurePos(
        OrderChannel channel,
        PosServiceType? serviceType,
        Guid? salonTableId,
        Guid? tableSessionId,
        Guid? servedByAdminUserId)
    {
        Channel = channel;
        ServiceType = serviceType;
        SalonTableId = salonTableId;
        TableSessionId = tableSessionId;
        ServedByAdminUserId = servedByAdminUserId;
    }

    public void ConfigureSplitBill(SplitBillMode splitBillMode, int? guestCount)
    {
        SplitBillMode = splitBillMode;
        IsSplitBill = splitBillMode != SplitBillMode.Single;
        GuestCount = IsSplitBill ? guestCount : null;
    }

    public void Hold()
    {
        if (Status is OrderStatus.Paid or OrderStatus.Cancelled or OrderStatus.Failed)
        {
            throw new DomainException("This order cannot be held.");
        }

        Status = OrderStatus.Held;
        HeldAt = DateTimeOffset.UtcNow;
    }

    public void ResumeFromHold()
    {
        if (Status != OrderStatus.Held)
        {
            throw new DomainException("Only held orders can be resumed.");
        }

        Status = OrderStatus.PendingPayment;
        HeldAt = null;
    }

    public void PayAtPos(PosPaymentMethod paymentMethod)
    {
        PaymentMethod = paymentMethod;

        if (Status == OrderStatus.Held)
        {
            Status = OrderStatus.PendingPayment;
            HeldAt = null;
        }

        MarkPaid();
    }

    public void UpdateCustomerForPos(string? customerName, string? customerPhone, string? note)
    {
        CustomerName = string.IsNullOrWhiteSpace(customerName) ? "مهمان" : customerName.Trim();
        CustomerPhone = string.IsNullOrWhiteSpace(customerPhone) ? "POS" : customerPhone.Trim();
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }

    public static Order CreatePosDraft(
        string trackingCode,
        string? customerName,
        string? customerPhone,
        string? note,
        decimal totalAmount,
        PosServiceType serviceType,
        Guid? salonTableId,
        Guid? tableSessionId,
        Guid? servedByAdminUserId,
        SplitBillMode splitBillMode = SplitBillMode.Single,
        int? guestCount = null)
    {
        var order = new Order
        {
            TrackingCode = trackingCode.Trim(),
            CustomerName = string.IsNullOrWhiteSpace(customerName) ? "مهمان" : customerName.Trim(),
            CustomerPhone = string.IsNullOrWhiteSpace(customerPhone) ? "POS" : customerPhone.Trim(),
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            TotalAmount = totalAmount,
            PaymentAuthority = $"POS-{Guid.NewGuid():N}",
            SplitBillMode = splitBillMode,
            IsSplitBill = splitBillMode != SplitBillMode.Single,
            GuestCount = splitBillMode != SplitBillMode.Single ? guestCount : null,
            Status = OrderStatus.Held,
            AmountPaid = 0,
            Channel = OrderChannel.Pos,
            ServiceType = serviceType,
            SalonTableId = salonTableId,
            TableSessionId = tableSessionId,
            ServedByAdminUserId = servedByAdminUserId,
            HeldAt = DateTimeOffset.UtcNow,
        };

        return order;
    }

    public void ReconcilePaymentStatus()
    {
        if (Status == OrderStatus.Cancelled || Status == OrderStatus.Failed)
        {
            return;
        }

        if (AmountPaid >= TotalAmount)
        {
            Status = OrderStatus.Paid;
            PaidAt ??= DateTimeOffset.UtcNow;
            AmountPaid = TotalAmount;
            RegisterForFulfillment();
            return;
        }

        if (AmountPaid > 0)
        {
            Status = OrderStatus.PartiallyPaid;
            PaidAt = null;
            return;
        }

        Status = OrderStatus.PendingPayment;
        PaidAt = null;
    }

    private void SetTrackingCode(string trackingCode)
    {
        if (string.IsNullOrWhiteSpace(trackingCode))
        {
            throw new DomainException("Order tracking code is required.");
        }

        TrackingCode = trackingCode.Trim();
    }

    private void SetTotalAmount(decimal totalAmount)
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
