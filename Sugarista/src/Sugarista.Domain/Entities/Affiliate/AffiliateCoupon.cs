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
        bool isActive = true)
    {
        if (affiliateId == Guid.Empty)
        {
            throw new DomainException("شناسه بازاریاب برای کد تخفیف الزامی است.");
        }

        AffiliateId = affiliateId;
        Update(code, discountType, discountValue, maxDiscountAmount, isFirstOrderOnly, usageLimitPerUser, isActive);
    }

    public Guid AffiliateId { get; private set; }

    public Affiliate Affiliate { get; private set; } = null!;

    public string Code { get; private set; } = string.Empty;

    public AffiliateDiscountType DiscountType { get; private set; }

    public decimal DiscountValue { get; private set; }

    public decimal? MaxDiscountAmount { get; private set; }

    public bool IsFirstOrderOnly { get; private set; }

    public int? UsageLimitPerUser { get; private set; }

    public bool IsActive { get; private set; } = true;

    public void Update(
        string code,
        AffiliateDiscountType discountType,
        decimal discountValue,
        decimal? maxDiscountAmount,
        bool isFirstOrderOnly,
        int? usageLimitPerUser,
        bool isActive)
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

        Code = code.Trim().ToUpperInvariant();
        DiscountType = discountType;
        DiscountValue = discountValue;
        MaxDiscountAmount = maxDiscountAmount;
        IsFirstOrderOnly = isFirstOrderOnly;
        UsageLimitPerUser = usageLimitPerUser;
        IsActive = isActive;
    }

    public void SetActive(bool isActive) => IsActive = isActive;
}
