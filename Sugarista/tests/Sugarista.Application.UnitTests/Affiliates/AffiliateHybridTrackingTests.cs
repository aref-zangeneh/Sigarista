using Sugarista.Application.Affiliates.Services;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.UnitTests.Affiliates;

public class AffiliateHybridTrackingTests
{
    [Fact]
    public void ResolveAttribution_CouponAlwaysWinsOverCookie()
    {
        var couponAffiliateId = Guid.NewGuid();
        var cookieAffiliateId = Guid.NewGuid();

        var result = AffiliateCommissionCalculator.ResolveAttribution(couponAffiliateId, cookieAffiliateId);

        Assert.Equal(couponAffiliateId, result.AffiliateId);
        Assert.Equal(AffiliateAttributionSource.Coupon, result.Source);
    }

    [Fact]
    public void ResolveAttribution_FallsBackToCookieWhenNoCoupon()
    {
        var cookieAffiliateId = Guid.NewGuid();

        var result = AffiliateCommissionCalculator.ResolveAttribution(null, cookieAffiliateId);

        Assert.Equal(cookieAffiliateId, result.AffiliateId);
        Assert.Equal(AffiliateAttributionSource.Cookie, result.Source);
    }

    [Fact]
    public void CalculateCouponDiscount_HandlesPercentageCapAndFixedAmount()
    {
        var affiliateId = Guid.NewGuid();

        var pctWithCap = new AffiliateCoupon(
            affiliateId, "ALI15", AffiliateDiscountType.Percentage, 15m, maxDiscountAmount: 50_000m);
        Assert.Equal(30_000m, AffiliateCommissionCalculator.CalculateCouponDiscount(pctWithCap, 200_000m));
        Assert.Equal(50_000m, AffiliateCommissionCalculator.CalculateCouponDiscount(pctWithCap, 500_000m));

        var fixedCoupon = new AffiliateCoupon(
            affiliateId, "CAFE40", AffiliateDiscountType.FixedAmount, 40_000m);
        Assert.Equal(40_000m, AffiliateCommissionCalculator.CalculateCouponDiscount(fixedCoupon, 100_000m));
        Assert.Equal(25_000m, AffiliateCommissionCalculator.CalculateCouponDiscount(fixedCoupon, 25_000m));

        pctWithCap.SetActive(false);
        Assert.Equal(0m, AffiliateCommissionCalculator.CalculateCouponDiscount(pctWithCap, 200_000m));
    }

    [Fact]
    public void CalculateOrderCommission_ComputesNetAfterDiscountPerCategory_ExcludingTaxAndDelivery()
    {
        var drinksCategoryId = Guid.NewGuid();
        var cakesCategoryId = Guid.NewGuid();

        var tier = new AffiliateTier("نقره‌ای", 10_000_000m, 8m, 15m);
        var affiliate = new Affiliate(Guid.NewGuid(), tierId: tier.Id, status: AffiliateStatus.Active);

        var lines = new List<AffiliateCommissionLineInput>
        {
            new(Guid.NewGuid(), drinksCategoryId, 200_000m),
            new(Guid.NewGuid(), cakesCategoryId, 100_000m),
        };

        var categoryRates = new Dictionary<Guid, decimal>
        {
            [drinksCategoryId] = 12m, // Drinks: 12%
            [cakesCategoryId] = 5m,   // Cakes: 5%
        };

        // 30,000 discount on 300,000 gross items -> 20,000 off drinks (net 180,000), 10,000 off cake (net 90,000)
        // Tax & delivery fee are NOT passed in, so EligibleAmount = 270,000
        var result = AffiliateCommissionCalculator.CalculateOrderCommission(
            affiliate,
            tier,
            lines,
            totalDiscountAmount: 30_000m,
            categoryRates);

        Assert.Equal(270_000m, result.EligibleAmount);
        // Drinks: 180,000 * 12% = 21,600; Cake: 90,000 * 5% = 4,500 => 26,100
        Assert.Equal(26_100m, result.CommissionAmount);
        Assert.Equal(2, result.Lines.Count);
        Assert.Equal(EffectiveCommissionSource.CategoryRate, result.Lines[0].RateSource);
    }

    [Fact]
    public void CalculateOrderCommission_VipCustomRateOverridesCategoryAndTierRates()
    {
        var drinksCategoryId = Guid.NewGuid();
        var cakesCategoryId = Guid.NewGuid();

        var tier = new AffiliateTier("برنزی", 0m, 5m, 10m);
        var vipAffiliate = new Affiliate(
            Guid.NewGuid(),
            tierId: tier.Id,
            status: AffiliateStatus.Active,
            customCommissionRate: 15m);

        var lines = new List<AffiliateCommissionLineInput>
        {
            new(Guid.NewGuid(), drinksCategoryId, 200_000m),
            new(Guid.NewGuid(), cakesCategoryId, 100_000m),
        };

        var categoryRates = new Dictionary<Guid, decimal>
        {
            [drinksCategoryId] = 12m,
            [cakesCategoryId] = 5m,
        };

        var result = AffiliateCommissionCalculator.CalculateOrderCommission(
            vipAffiliate,
            tier,
            lines,
            totalDiscountAmount: 30_000m,
            categoryRates);

        Assert.Equal(270_000m, result.EligibleAmount);
        // 180,000 * 15% = 27,000; 90,000 * 15% = 13,500 => 40,500
        Assert.Equal(40_500m, result.CommissionAmount);
        Assert.All(result.Lines, line => Assert.Equal(EffectiveCommissionSource.CustomVipOverride, line.RateSource));
    }
}
