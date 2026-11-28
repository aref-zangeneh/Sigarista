using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

public class AffiliateCoupon : BaseAuditableEntity
{
    private AffiliateCoupon()
    {
    }

    public AffiliateCoupon(
        Guid affiliateId,
        string code,
        AffiliateDiscountType discountType,
        decimal discountValue,
        decimal? maxDiscountAmount = null,
        bool isFirstOrderOnly = false,
        int? usageLimitPerUser = null,
        bool isActive = true,
        int? maxTotalUsages = null,
        DateTimeOffset? startsAt = null,
        DateTimeOffset? expiresAt = null)
    {
        if (affiliateId == Guid.Empty)
        {
            throw new DomainException("شناسه بازاریاب برای کد تخفیف الزامی است.");
        }

        AffiliateId = affiliateId;
        Update(
            code,
            discountType,
            discountValue,
            maxDiscountAmount,
            isFirstOrderOnly,
            usageLimitPerUser,
            isActive,
            maxTotalUsages,
            startsAt,
            expiresAt);
    }

    public Guid AffiliateId { get; private set; }

    public Affiliate Affiliate { get; private set; } = null!;

    public string Code { get; private set; } = string.Empty;

    public AffiliateDiscountType DiscountType { get; private set; }

    public decimal DiscountValue { get; private set; }

    public decimal? MaxDiscountAmount { get; private set; }

    public bool IsFirstOrderOnly { get; private set; }

    public int? UsageLimitPerUser { get; private set; }

    /// <summary>Optional global maximum number of redemptions allowed across all customers (null = unlimited).</summary>
    public int? MaxTotalUsages { get; private set; }

    /// <summary>Optional start timestamp (UTC) from which the coupon becomes valid (null = immediately active).</summary>
    public DateTimeOffset? StartsAt { get; private set; }

    /// <summary>Optional expiration timestamp (UTC) after which the coupon can no longer be used (null = never expires).</summary>
    public DateTimeOffset? ExpiresAt { get; private set; }

    public bool IsActive { get; private set; } = true;

    public void Update(
        string code,
        AffiliateDiscountType discountType,
        decimal discountValue,
        decimal? maxDiscountAmount,
        bool isFirstOrderOnly,
        int? usageLimitPerUser,
        bool isActive,
        int? maxTotalUsages = null,
        DateTimeOffset? startsAt = null,
        DateTimeOffset? expiresAt = null)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new DomainException("کد تخفیف بازاریاب الزامی است.");
        }

        if (discountValue <= 0m)
        {
            throw new DomainException("مقدار تخفیف باید بزرگ‌تر از صفر باشد.");
        }

        if (discountType == AffiliateDiscountType.Percentage && discountValue > 100m)
        {
            throw new DomainException("درصد تخفیف نمی‌تواند بیشتر از ۱۰۰ باشد.");
        }

        if (maxDiscountAmount.HasValue && maxDiscountAmount.Value <= 0m)
        {
            throw new DomainException("سقف مبلغ تخفیف باید بزرگ‌تر از صفر باشد.");
        }

        if (usageLimitPerUser.HasValue && usageLimitPerUser.Value <= 0)
        {
            throw new DomainException("محدودیت استفاده برای هر کاربر باید بزرگ‌تر از صفر باشد.");
        }

        if (maxTotalUsages.HasValue && maxTotalUsages.Value <= 0)
        {
            throw new DomainException("سقف کل تعداد استفاده باید بزرگ‌تر از صفر باشد.");
        }

        if (startsAt.HasValue && expiresAt.HasValue && expiresAt.Value <= startsAt.Value)
        {
            throw new DomainException("تاریخ پایان اعتبار کد تخفیف باید بعد از تاریخ شروع باشد.");
        }

        Code = code.Trim().ToUpperInvariant();
        DiscountType = discountType;
        DiscountValue = discountValue;
        MaxDiscountAmount = maxDiscountAmount;
        IsFirstOrderOnly = isFirstOrderOnly;
        UsageLimitPerUser = usageLimitPerUser;
        MaxTotalUsages = maxTotalUsages;
        StartsAt = startsAt;
        ExpiresAt = expiresAt;
        IsActive = isActive;
    }

    public void SetActive(bool isActive) => IsActive = isActive;
}
