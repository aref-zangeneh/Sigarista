using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

public class AffiliateCommission : BaseAuditableEntity
{
    private AffiliateCommission()
    {
    }

    public AffiliateCommission(
        Guid affiliateId,
        Guid orderId,
        AffiliateOrderSource orderSource,
        decimal orderTotalAmount,
        decimal eligibleAmount,
        decimal commissionAmount,
        AffiliateCommissionStatus status = AffiliateCommissionStatus.Pending,
        string? calculationSnapshotJson = null)
    {
        if (affiliateId == Guid.Empty)
        {
            throw new DomainException("شناسه بازاریاب الزامی است.");
        }

        if (orderId == Guid.Empty)
        {
            throw new DomainException("شناسه سفارش الزامی است.");
        }

        if (orderTotalAmount < 0m)
        {
            throw new DomainException("مبلغ کل سفارش نمی‌تواند منفی باشد.");
        }

        if (eligibleAmount < 0m)
        {
            throw new DomainException("مبلغ مشمول پورسانت نمی‌تواند منفی باشد.");
        }

        if (commissionAmount < 0m)
        {
            throw new DomainException("مبلغ پورسانت نمی‌تواند منفی باشد.");
        }

        AffiliateId = affiliateId;
        OrderId = orderId;
        OrderSource = orderSource;
        OrderTotalAmount = orderTotalAmount;
        EligibleAmount = eligibleAmount;
        CommissionAmount = commissionAmount;
        Status = status;
        CalculationSnapshotJson = string.IsNullOrWhiteSpace(calculationSnapshotJson)
            ? null
            : calculationSnapshotJson;
    }

    public Guid AffiliateId { get; private set; }

    public Affiliate Affiliate { get; private set; } = null!;

    public Guid OrderId { get; private set; }

    public Order Order { get; private set; } = null!;

    public AffiliateOrderSource OrderSource { get; private set; }

    public decimal OrderTotalAmount { get; private set; }

    public decimal EligibleAmount { get; private set; }

    public decimal CommissionAmount { get; private set; }

    public AffiliateCommissionStatus Status { get; private set; } = AffiliateCommissionStatus.Pending;

    public string? RejectionReason { get; private set; }

    /// <summary>
    /// Immutable JSON snapshot locking item prices, category commission rates,
    /// tier/VIP rates, and net eligible amounts at the exact moment of order creation.
    /// Guarantees future price or rate changes never retroactively alter commissions.
    /// </summary>
    public string? CalculationSnapshotJson { get; private set; }

    public void Approve()
    {
        if (Status != AffiliateCommissionStatus.Pending)
        {
            throw new DomainException("تنها پورسانت‌های در انتظار تایید قابل تایید هستند.");
        }

        Status = AffiliateCommissionStatus.Approved;
        RejectionReason = null;
    }

    public void Reject(string reason)
    {
        if (Status == AffiliateCommissionStatus.Paid)
        {
            throw new DomainException("پورسانت پرداخت‌شده قابل رد کردن نیست.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException("ذکر دلیل رد پورسانت الزامی است.");
        }

        Status = AffiliateCommissionStatus.Rejected;
        RejectionReason = reason.Trim();
    }

    public void MarkPaid()
    {
        if (Status != AffiliateCommissionStatus.Approved)
        {
            throw new DomainException("تنها پورسانت‌های تاییدشده می‌توانند به وضعیت پرداخت‌شده تغییر کنند.");
        }

        Status = AffiliateCommissionStatus.Paid;
    }
}
