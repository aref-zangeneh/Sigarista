using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

public enum ManualDiscountType
{
    Amount = 1,
    Percent = 2,
}

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

    /// <summary>Confidential note visible and editable strictly by cafe management; never printed on receipts.</summary>
    public string? ManagerNote { get; private set; }

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

    /// <summary>Distinguishes normal revenue orders from internal consumption or complimentary guest orders.</summary>
    public OrderKind OrderKind { get; private set; } = OrderKind.Standard;

    /// <summary>Optional reason recorded for internal consumption or complimentary/guest orders.</summary>
    public string? ComplimentaryReason { get; private set; }

    /// <summary>When true, this order is excluded from accounting journal entries and financial/sales summaries.</summary>
    public bool IsExcludedFromAccounting { get; private set; }

    public DateTimeOffset? ExcludedFromAccountingAt { get; private set; }

    public string? ExcludedFromAccountingReason { get; private set; }

    public bool IsNonRevenue => OrderKind is OrderKind.InternalUse or OrderKind.Complimentary;

    public Guid? SalonTableId { get; private set; }

    public SalonTable? SalonTable { get; private set; }

    public Guid? TableSessionId { get; private set; }

    public TableSession? TableSession { get; private set; }

    public Guid? ServedByAdminUserId { get; private set; }

    /// <summary>Delivery/service method applied to this order; null = no delivery method fee/packaging set.</summary>
    public Guid? DeliveryMethodId { get; private set; }

    public DeliveryMethod? DeliveryMethod { get; private set; }

    public decimal DeliveryFee { get; private set; }

    /// <summary>Scheduled delivery time chosen by the customer (null = as soon as possible).</summary>
    public DateTimeOffset? DeliveryAt { get; private set; }

    /// <summary>
    /// Client-generated idempotency key for online checkout. A unique index
    /// guarantees a double-click or retried request never creates two orders.
    /// </summary>
    public string? IdempotencyKey { get; private set; }

    /// <summary>
    /// How many times the customer has rescheduled this pickup order. The
    /// café can cap this via <see cref="SiteSettings.PickupMaxReschedules"/>.
    /// </summary>
    public int RescheduleCount { get; private set; }

    public void ApplyDeliveryTime(DateTimeOffset? deliveryAt)
    {
        // Always store the absolute instant in UTC: PostgreSQL timestamptz
        // parameters reject non-zero offsets and comparisons stay unambiguous.
        DeliveryAt = deliveryAt?.ToUniversalTime();
    }

    public void RecordReschedule()
    {
        RescheduleCount++;
    }

    public void SetIdempotencyKey(string? idempotencyKey)
    {
        IdempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey)
            ? null
            : idempotencyKey.Trim();
    }

    /// <summary>Unified customer profile linked by channel (POS / online) for the customer club.</summary>
    public Guid? CustomerId { get; private set; }

    public Customer? Customer { get; private set; }

    /// <summary>Computed tax of the order (sum of per-line tax based on each item's tax percent).</summary>
    public decimal TaxTotal { get; private set; }

    /// <summary>True once the delivery method's disposable packaging set has been deducted from stock.</summary>
    public bool PackagingDeducted { get; private set; }

    public DateTimeOffset? HeldAt { get; private set; }

    public ICollection<OrderLine> Lines { get; private set; } = [];

    public ICollection<OrderPayment> Payments { get; private set; } = [];

    public ICollection<OrderSplitShare> SplitShares { get; private set; } = [];

    /// <summary>Every benefit applied to this order (code, tier, group, cup, …).</summary>
    public ICollection<OrderDiscount> Discounts { get; private set; } = [];

    /// <summary>
    /// Sum of all discounts actually applied to this order. Kept separate from
    /// <see cref="TotalAmount"/> so reports and the daily per-customer cap can
    /// read the discount value directly.
    /// </summary>
    public decimal DiscountTotal { get; private set; }

    /// <summary>Type of on-the-spot POS manual discount applied (Amount or Percent), if any.</summary>
    public ManualDiscountType? ManualDiscountType { get; private set; }

    /// <summary>Raw manual discount value entered in POS (fixed Toman amount or percent 0-100).</summary>
    public decimal? ManualDiscountValue { get; private set; }

    /// <summary>Actual Toman amount deducted by the POS manual discount.</summary>
    public decimal ManualDiscountAmount { get; private set; }

    /// <summary>Optional reason/note recorded when applying a POS manual discount.</summary>
    public string? ManualDiscountReason { get; private set; }

    /// <summary>Human-readable name of the admin user who applied the POS manual discount.</summary>
    public string? ManualDiscountAppliedBy { get; private set; }

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

        if (IsNonRevenue)
        {
            TaxTotal = 0m;
            TotalAmount = 0m;
            DiscountTotal = 0m;
            ClearManualDiscount();
            return;
        }

        var itemsTotal = Lines.Sum(line => line.LineTotal);
        var taxTotal = Lines.Sum(line => Math.Round(line.LineTotal * line.TaxPercent / 100m, 2));
        TaxTotal = taxTotal;
        TotalAmount = itemsTotal + DeliveryFee + taxTotal;
        // A fresh total starts with no discounts; POS upserts and delivery
        // changes recompute from scratch so benefits never stack twice.
        DiscountTotal = 0m;
        ClearManualDiscount();
    }

    public void ClearManualDiscount()
    {
        ManualDiscountType = null;
        ManualDiscountValue = null;
        ManualDiscountAmount = 0m;
        ManualDiscountReason = null;
        ManualDiscountAppliedBy = null;
    }

    /// <summary>
    /// Applies an authorized on-the-spot POS manual discount (fixed Toman amount or percent)
    /// against the remaining payable order total and records audit metadata on the order.
    /// </summary>
    public decimal ApplyManualDiscount(
        ManualDiscountType type,
        decimal value,
        string? reason = null,
        string? appliedBy = null)
    {
        if (IsNonRevenue)
        {
            ClearManualDiscount();
            return 0m;
        }

        if (value <= 0m)
        {
            throw new DomainException("مقدار تخفیف دلخواه باید بزرگ‌تر از صفر باشد.");
        }

        decimal computed;
        if (type == Entities.ManualDiscountType.Percent)
        {
            if (value > 100m)
            {
                throw new DomainException("درصد تخفیف دلخواه باید بین ۰ تا ۱۰۰ باشد.");
            }

            computed = Math.Round(TotalAmount * value / 100m, 0, MidpointRounding.AwayFromZero);
        }
        else
        {
            if (value > TotalAmount)
            {
                throw new DomainException("مبلغ تخفیف دلخواه نمی‌تواند بیشتر از مبلغ قابل پرداخت سفارش باشد.");
            }

            computed = Math.Round(value, 0, MidpointRounding.AwayFromZero);
        }

        var applied = Math.Min(computed, TotalAmount);
        if (applied <= 0m)
        {
            ClearManualDiscount();
            return 0m;
        }

        ApplyDiscount(applied);
        ManualDiscountType = type;
        ManualDiscountValue = value;
        ManualDiscountAmount = applied;
        ManualDiscountReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        ManualDiscountAppliedBy = string.IsNullOrWhiteSpace(appliedBy) ? null : appliedBy.Trim();
        return applied;
    }

    public void AssignCustomer(Guid? customerId)
    {
        CustomerId = customerId;
    }

    /// <summary>
    /// Applies a server-computed discount (from the discount engine) to the
    /// final order total. Runs only inside the backend domain — the frontend
    /// can never set or override the final amount.
    /// </summary>
    public void ApplyDiscount(decimal amount)    {
        if (amount < 0)
        {
            throw new DomainException("Discount amount cannot be negative.");
        }

        var applied = Math.Min(amount, TotalAmount);
        TotalAmount -= applied;
        DiscountTotal += applied;
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

    /// <summary>
    /// Reduces the outstanding payable by the amount the customer just paid
    /// from their wallet (server-side only). Distinct from discounts: the
    /// money was pre-charged, so it is payment, not a price reduction.
    /// </summary>
    public void ApplyWalletPayment(decimal amount)
    {
        if (amount < 0)
        {
            throw new DomainException("Wallet payment amount cannot be negative.");
        }

        TotalAmount = Math.Max(0, TotalAmount - amount);
    }

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

    public void SetManagerNote(string? managerNote)
    {
        if (string.IsNullOrWhiteSpace(managerNote))
        {
            ManagerNote = null;
            return;
        }

        var trimmed = managerNote.Trim();
        if (trimmed.Length > 1000)
        {
            throw new DomainException("یادداشت مدیریت نمی‌تواند بیش از ۱۰۰۰ کاراکتر باشد.");
        }

        ManagerNote = trimmed;
    }

    public static string GetPaymentMethodLabel(PosPaymentMethod method) => method switch
    {
        PosPaymentMethod.Cash => "نقدی",
        PosPaymentMethod.Card => "کارت‌خوان (پوز)",
        PosPaymentMethod.CardToCard => "کارت به کارت",
        PosPaymentMethod.Split => "پرداخت ترکیبی",
        PosPaymentMethod.OnlineGateway => "درگاه اینترنتی",
        PosPaymentMethod.Complimentary => "رایگان",
        _ => method.ToString()
    };

    public void PayAtPos(PosPaymentMethod paymentMethod)
    {
        if (paymentMethod == PosPaymentMethod.Complimentary)
        {
            throw new DomainException("Use CompleteWithoutCharge for complimentary or internal consumption orders.");
        }

        OrderKind = OrderKind.Standard;
        ComplimentaryReason = null;
        PaymentMethod = paymentMethod;

        if (Status == OrderStatus.Held)
        {
            Status = OrderStatus.PendingPayment;
            HeldAt = null;
        }

        MarkPaid();
    }

    public IReadOnlyList<OrderPayment> RecordPosPayments(
        IReadOnlyCollection<(PosPaymentMethod Method, decimal Amount, string? Reference)> allocations)
    {
        if (allocations is null || allocations.Count == 0)
        {
            throw new DomainException("حداقل یک روش پرداخت باید مشخص شود.");
        }

        var roundedTotal = Math.Round(TotalAmount, 2, MidpointRounding.AwayFromZero);
        foreach (var leg in allocations)
        {
            if (leg.Method is not (PosPaymentMethod.Cash or PosPaymentMethod.Card or PosPaymentMethod.CardToCard))
            {
                throw new DomainException("روش پرداخت در تسویه صندوق فقط می‌تواند نقدی، کارت‌خوان یا کارت به کارت باشد.");
            }

            if (leg.Amount < 0m || (roundedTotal > 0m && leg.Amount <= 0m))
            {
                throw new DomainException("مبلغ هر بخش از پرداخت باید بزرگ‌تر از صفر باشد.");
            }
        }

        var sum = Math.Round(allocations.Sum(leg => leg.Amount), 2, MidpointRounding.AwayFromZero);
        if (roundedTotal > 0m && sum != roundedTotal)
        {
            throw new DomainException(
                $"مجموع مبالغ پرداختی ({sum:N0} تومان) باید دقیقاً برابر مبلغ قابل پرداخت سفارش ({roundedTotal:N0} تومان) باشد.");
        }

        var positiveLegs = allocations.Where(leg => leg.Amount > 0m).ToList();
        var distinctMethods = positiveLegs.Select(leg => leg.Method).Distinct().ToList();
        var effectiveMethod = distinctMethods.Count switch
        {
            0 => allocations.First().Method,
            1 => distinctMethods[0],
            _ => PosPaymentMethod.Split
        };

        PayAtPos(effectiveMethod);

        var createdPayments = new List<OrderPayment>(positiveLegs.Count);
        var index = 1;
        foreach (var leg in positiveLegs)
        {
            var payment = new OrderPayment(
                Id,
                payerKey: $"pos-{index}",
                payerLabel: GetPaymentMethodLabel(leg.Method),
                amount: Math.Round(leg.Amount, 2, MidpointRounding.AwayFromZero),
                paymentAuthority: $"POS-{leg.Method}-{Guid.NewGuid():N}",
                paymentMethod: leg.Method);
            payment.MarkPaid(string.IsNullOrWhiteSpace(leg.Reference) ? null : leg.Reference);
            Payments.Add(payment);
            createdPayments.Add(payment);
            index++;
        }

        return createdPayments;
    }

    public void ConfigureOrderKind(OrderKind orderKind, string? reason = null)
    {
        OrderKind = orderKind;
        ComplimentaryReason = orderKind == OrderKind.Standard || string.IsNullOrWhiteSpace(reason)
            ? null
            : reason.Trim();
    }

    public void CompleteWithoutCharge(OrderKind orderKind, string? reason = null)
    {
        if (orderKind is not (OrderKind.InternalUse or OrderKind.Complimentary))
        {
            throw new DomainException("Only InternalUse or Complimentary orders can be completed without charge.");
        }

        if (Status == OrderStatus.Held)
        {
            Status = OrderStatus.PendingPayment;
            HeldAt = null;
        }

        if (Status is not (OrderStatus.PendingPayment or OrderStatus.PartiallyPaid))
        {
            throw new DomainException("Only pending, held, or partially paid orders can be completed without charge.");
        }

        OrderKind = orderKind;
        ComplimentaryReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        PaymentMethod = PosPaymentMethod.Complimentary;
        TotalAmount = 0m;
        AmountPaid = 0m;
        TaxTotal = 0m;
        DiscountTotal = 0m;
        ClearManualDiscount();
        Status = OrderStatus.Paid;
        PaidAt = DateTimeOffset.UtcNow;
        RegisterForFulfillment();
    }

    public void ExcludeFromAccounting(string? reason = null)
    {
        IsExcludedFromAccounting = true;
        ExcludedFromAccountingAt = DateTimeOffset.UtcNow;
        ExcludedFromAccountingReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
    }

    public void IncludeInAccounting()
    {
        IsExcludedFromAccounting = false;
        ExcludedFromAccountingAt = null;
        ExcludedFromAccountingReason = null;
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
