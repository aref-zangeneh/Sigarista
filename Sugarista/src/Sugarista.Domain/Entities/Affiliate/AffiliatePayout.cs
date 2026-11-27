using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

public class AffiliatePayout : BaseAuditableEntity
{
    private AffiliatePayout()
    {
    }

    public AffiliatePayout(
        Guid affiliateId,
        decimal amount,
        AffiliatePayoutType payoutType)
    {
        if (affiliateId == Guid.Empty)
        {
            throw new DomainException("شناسه بازاریاب برای تسویه الزامی است.");
        }

        if (amount <= 0m)
        {
            throw new DomainException("مبلغ تسویه باید بزرگ‌تر از صفر باشد.");
        }

        AffiliateId = affiliateId;
        Amount = amount;
        PayoutType = payoutType;
        Status = AffiliatePayoutStatus.Requested;
    }

    public Guid AffiliateId { get; private set; }

    public Affiliate Affiliate { get; private set; } = null!;

    public decimal Amount { get; private set; }

    public AffiliatePayoutType PayoutType { get; private set; }

    public AffiliatePayoutStatus Status { get; private set; } = AffiliatePayoutStatus.Requested;

    public string? TrackingCode { get; private set; }

    public string? ReceiptImageUrl { get; private set; }

    public DateTimeOffset? ProcessedAt { get; private set; }

    public void MarkProcessing()
    {
        if (Status != AffiliatePayoutStatus.Requested)
        {
            throw new DomainException("تنها درخواست‌های جدید قابل انتقال به وضعیت در حال پردازش هستند.");
        }

        Status = AffiliatePayoutStatus.Processing;
    }

    public void Complete(string? trackingCode, string? receiptImageUrl, DateTimeOffset processedAt)
    {
        if (Status is not (AffiliatePayoutStatus.Requested or AffiliatePayoutStatus.Processing))
        {
            throw new DomainException("این درخواست تسویه قابل تکمیل نیست.");
        }

        Status = AffiliatePayoutStatus.Completed;
        TrackingCode = string.IsNullOrWhiteSpace(trackingCode) ? null : trackingCode.Trim();
        ReceiptImageUrl = string.IsNullOrWhiteSpace(receiptImageUrl) ? null : receiptImageUrl.Trim();
        ProcessedAt = processedAt;
    }

    public void Reject(DateTimeOffset processedAt)
    {
        if (Status == AffiliatePayoutStatus.Completed)
        {
            throw new DomainException("تسویه تکمیل‌شده قابل رد کردن نیست.");
        }

        Status = AffiliatePayoutStatus.Rejected;
        ProcessedAt = processedAt;
    }
}
