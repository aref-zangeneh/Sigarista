using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

/// <summary>
/// Registered cafe affiliate/promoter linked to a Customer (UserId).
/// Tracks tier assignment, optional VIP custom commission rate override,
/// banking settlement info, and cash / cafe-credit balances.
/// </summary>
public class Affiliate : BaseAuditableEntity
{
    private Affiliate()
    {
    }

    public Affiliate(
        Guid userId,
        Guid? tierId = null,
        AffiliateStatus status = AffiliateStatus.Pending,
        string? shebaNumber = null,
        string? cardNumber = null,
        decimal? customCommissionRate = null)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("شناسه کاربر (مشتری) برای بازاریاب الزامی است.");
        }

        UserId = userId;
        TierId = tierId;
        Status = status;
        UpdateBankInfo(shebaNumber, cardNumber);
        SetCustomCommissionRate(customCommissionRate);
    }

    public Guid UserId { get; private set; }

    public Customer User { get; private set; } = null!;

    public Guid? TierId { get; private set; }

    public AffiliateTier? Tier { get; private set; }

    public AffiliateStatus Status { get; private set; } = AffiliateStatus.Pending;

    public string? ShebaNumber { get; private set; }

    public string? CardNumber { get; private set; }

    public decimal CashBalance { get; private set; }

    public decimal WalletCreditBalance { get; private set; }

    /// <summary>
    /// Optional fixed commission rate (%) set by admin for VIP influencers.
    /// When non-null, overrides progressive tier commission rules.
    /// </summary>
    public decimal? CustomCommissionRate { get; private set; }

    public ICollection<AffiliateLink> Links { get; private set; } = [];

    public ICollection<AffiliateCoupon> Coupons { get; private set; } = [];

    public ICollection<AffiliateCommission> Commissions { get; private set; } = [];

    public ICollection<AffiliatePayout> Payouts { get; private set; } = [];

    public void SetStatus(AffiliateStatus status) => Status = status;

    public void Activate() => Status = AffiliateStatus.Active;

    public void Suspend() => Status = AffiliateStatus.Suspended;

    public void AssignTier(Guid? tierId) => TierId = tierId;

    public void SetCustomCommissionRate(decimal? customCommissionRate)
    {
        if (customCommissionRate.HasValue && (customCommissionRate.Value < 0m || customCommissionRate.Value > 100m))
        {
            throw new DomainException("نرخ پورسانت اختصاصی باید بین ۰ تا ۱۰۰ درصد باشد.");
        }

        CustomCommissionRate = customCommissionRate;
    }

    public void UpdateBankInfo(string? shebaNumber, string? cardNumber)
    {
        ShebaNumber = string.IsNullOrWhiteSpace(shebaNumber) ? null : shebaNumber.Trim().ToUpperInvariant();
        CardNumber = string.IsNullOrWhiteSpace(cardNumber) ? null : cardNumber.Trim();
    }

    public void CreditCashBalance(decimal amount)
    {
        if (amount <= 0m)
        {
            throw new DomainException("مبلغ واریز به موجودی نقدی باید مثبت باشد.");
        }

        CashBalance += amount;
    }

    public void DebitCashBalance(decimal amount)
    {
        if (amount <= 0m)
        {
            throw new DomainException("مبلغ برداشت از موجودی نقدی باید مثبت باشد.");
        }

        if (amount > CashBalance)
        {
            throw new DomainException("موجودی نقدی بازاریاب کافی نیست.");
        }

        CashBalance -= amount;
    }

    public void CreditWalletCreditBalance(decimal amount)
    {
        if (amount <= 0m)
        {
            throw new DomainException("مبلغ واریز به اعتبار کافه باید مثبت باشد.");
        }

        WalletCreditBalance += amount;
    }

    public void DebitWalletCreditBalance(decimal amount)
    {
        if (amount <= 0m)
        {
            throw new DomainException("مبلغ کسر از اعتبار کافه باید مثبت باشد.");
        }

        if (amount > WalletCreditBalance)
        {
            throw new DomainException("موجودی اعتبار کافه بازاریاب کافی نیست.");
        }

        WalletCreditBalance -= amount;
    }
}
