using Sugarista.Domain.Entities;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.UnitTests;

public class AffiliateEntitiesTests
{
    [Fact]
    public void AffiliateTier_ValidatesNameSalesAndPercentages()
    {
        var tier = new AffiliateTier("طلایی", 20_000_000m, 12m, 15m);
        Assert.Equal("طلایی", tier.Name);
        Assert.Equal(20_000_000m, tier.MinMonthlySales);
        Assert.Equal(12m, tier.CommissionPercentage);
        Assert.Equal(15m, tier.CreditBonusPercentage);

        Assert.Throws<DomainException>(() => new AffiliateTier("", 0m, 5m, 0m));
        Assert.Throws<DomainException>(() => new AffiliateTier("برنزی", -1m, 5m, 0m));
        Assert.Throws<DomainException>(() => new AffiliateTier("برنزی", 0m, 101m, 0m));
        Assert.Throws<DomainException>(() => new AffiliateTier("برنزی", 0m, 5m, -5m));
    }

    [Fact]
    public void Affiliate_BalancesNeverGoNegative_AndCustomRateValidated()
    {
        var affiliate = new Affiliate(Guid.NewGuid(), status: AffiliateStatus.Active);
        affiliate.CreditCashBalance(500_000m);
        affiliate.DebitCashBalance(200_000m);
        Assert.Equal(300_000m, affiliate.CashBalance);
        Assert.Throws<DomainException>(() => affiliate.DebitCashBalance(300_001m));

        affiliate.CreditWalletCreditBalance(100_000m);
        affiliate.DebitWalletCreditBalance(100_000m);
        Assert.Equal(0m, affiliate.WalletCreditBalance);
        Assert.Throws<DomainException>(() => affiliate.DebitWalletCreditBalance(1m));

        affiliate.SetCustomCommissionRate(18.5m);
        Assert.Equal(18.5m, affiliate.CustomCommissionRate);
        affiliate.SetCustomCommissionRate(null);
        Assert.Null(affiliate.CustomCommissionRate);
        Assert.Throws<DomainException>(() => affiliate.SetCustomCommissionRate(101m));
        Assert.Throws<DomainException>(() => affiliate.SetCustomCommissionRate(-1m));
    }

    [Fact]
    public void AffiliateLink_NormalizesSlugAndSubId_AndIncrementsClicks()
    {
        var link = new AffiliateLink(Guid.NewGuid(), " Ali ", "https://sugarista.ir/menu", " Story1 ");
        Assert.Equal("ali", link.Slug);
        Assert.Equal("story1", link.SubId);
        Assert.Equal(0, link.ClickCount);

        link.IncrementClickCount();
        link.IncrementClickCount();
        Assert.Equal(2, link.ClickCount);
    }

    [Fact]
    public void AffiliateCoupon_NormalizesCode_AndValidatesDiscountRules()
    {
        var coupon = new AffiliateCoupon(
            Guid.NewGuid(),
            " ali15 ",
            AffiliateDiscountType.Percentage,
            15m,
            maxDiscountAmount: 100_000m,
            isFirstOrderOnly: true,
            usageLimitPerUser: 1);

        Assert.Equal("ALI15", coupon.Code);
        Assert.True(coupon.IsFirstOrderOnly);
        Assert.True(coupon.IsActive);

        Assert.Throws<DomainException>(() => new AffiliateCoupon(
            Guid.NewGuid(), "BAD", AffiliateDiscountType.Percentage, 120m));
        Assert.Throws<DomainException>(() => new AffiliateCoupon(
            Guid.NewGuid(), "BAD", AffiliateDiscountType.FixedAmount, 0m));
    }

    [Fact]
    public void ReferralClick_TracksExpirationAccurately()
    {
        var now = DateTimeOffset.UtcNow;
        var click = new ReferralClick(Guid.NewGuid(), Guid.NewGuid(), now.AddDays(30), "1.2.3.4", "Mozilla/5.0");

        Assert.False(click.IsExpired(now.AddDays(29)));
        Assert.True(click.IsExpired(now.AddDays(30)));
    }

    [Fact]
    public void AffiliateCommission_EnforcesValidStatusTransitions()
    {
        var commission = new AffiliateCommission(
            Guid.NewGuid(),
            Guid.NewGuid(),
            AffiliateOrderSource.Online,
            500_000m,
            450_000m,
            45_000m);

        Assert.Equal(AffiliateCommissionStatus.Pending, commission.Status);
        Assert.Throws<DomainException>(() => commission.MarkPaid()); // Must be Approved first

        commission.Approve();
        Assert.Equal(AffiliateCommissionStatus.Approved, commission.Status);
        Assert.Throws<DomainException>(() => commission.Approve()); // Already approved

        commission.MarkPaid();
        Assert.Equal(AffiliateCommissionStatus.Paid, commission.Status);
        Assert.Throws<DomainException>(() => commission.Reject("لغو سفارش")); // Cannot reject paid
    }

    [Fact]
    public void AffiliatePayout_EnforcesValidStatusTransitions()
    {
        var payout = new AffiliatePayout(Guid.NewGuid(), 250_000m, AffiliatePayoutType.Cash);
        Assert.Equal(AffiliatePayoutStatus.Requested, payout.Status);

        payout.MarkProcessing();
        Assert.Equal(AffiliatePayoutStatus.Processing, payout.Status);

        var processedAt = DateTimeOffset.UtcNow;
        payout.Complete("TRK-998877", "/uploads/receipt.jpg", processedAt);
        Assert.Equal(AffiliatePayoutStatus.Completed, payout.Status);
        Assert.Equal("TRK-998877", payout.TrackingCode);
        Assert.Equal(processedAt, payout.ProcessedAt);

        Assert.Throws<DomainException>(() => payout.Reject(processedAt));
    }

    [Fact]
    public void CategoryCommission_ValidatesPercentageBounds()
    {
        var cc = new CategoryCommission(Guid.NewGuid(), 12.5m);
        Assert.Equal(12.5m, cc.CommissionPercentage);

        Assert.Throws<DomainException>(() => new CategoryCommission(Guid.Empty, 10m));
        Assert.Throws<DomainException>(() => cc.SetCommissionPercentage(-0.1m));
        Assert.Throws<DomainException>(() => cc.SetCommissionPercentage(100.1m));
    }
}
