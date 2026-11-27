using Sugarista.Application.Affiliates.Services;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.UnitTests.Affiliates;

public class AffiliateTierEngineTests
{
    private static List<AffiliateTier> CreateDefaultTiers() =>
    [
        new AffiliateTier("برنزی", 0m, 5m, 10m),
        new AffiliateTier("نقره‌ای", 10_000_000m, 8m, 15m),
        new AffiliateTier("طلایی", 25_000_000m, 12m, 20m),
    ];

    [Theory]
    [InlineData(0, "برنزی")]
    [InlineData(9_999_999, "برنزی")]
    [InlineData(10_000_000, "نقره‌ای")]
    [InlineData(24_999_999, "نقره‌ای")]
    [InlineData(25_000_000, "طلایی")]
    [InlineData(100_000_000, "طلایی")]
    public void ResolveTierForSales_UpgradesAndDowngradesCorrectly(decimal rolling30DaySales, string expectedTierName)
    {
        var tiers = CreateDefaultTiers();

        var resolved = AffiliateTierCalculator.ResolveTierForSales(rolling30DaySales, tiers);

        Assert.NotNull(resolved);
        Assert.Equal(expectedTierName, resolved!.Name);
    }

    [Fact]
    public void ResolveTierForSales_DowngradesWhenSalesDropBelowThreshold()
    {
        var tiers = CreateDefaultTiers();

        // Month 1: Gold (30M)
        var month1Tier = AffiliateTierCalculator.ResolveTierForSales(30_000_000m, tiers);
        Assert.Equal("طلایی", month1Tier!.Name);

        // Month 2: sales dropped to 4M -> automatically downgrades to Bronze
        var month2Tier = AffiliateTierCalculator.ResolveTierForSales(4_000_000m, tiers);
        Assert.Equal("برنزی", month2Tier!.Name);
    }

    [Fact]
    public void ResolveEffectiveCommissionRate_VipCustomRateOverridesTierAndCategory()
    {
        var tiers = CreateDefaultTiers();
        var bronze = tiers[0]; // 5%
        var vipAffiliate = new Affiliate(
            Guid.NewGuid(),
            tierId: bronze.Id,
            status: AffiliateStatus.Active,
            customCommissionRate: 18m);

        // VIP custom rate (18%) must override BOTH Bronze tier (5%) AND Category override (10%)
        var resolved = AffiliateTierCalculator.ResolveEffectiveCommissionRate(
            vipAffiliate,
            bronze,
            categoryCommissionPercentage: 10m);

        Assert.Equal(18m, resolved.RatePercentage);
        Assert.Equal(EffectiveCommissionSource.CustomVipOverride, resolved.Source);
    }

    [Fact]
    public void ResolveEffectiveCommissionRate_FallsBackToCategoryThenTierWhenNoCustomRate()
    {
        var tiers = CreateDefaultTiers();
        var silver = tiers[1]; // 8%
        var regularAffiliate = new Affiliate(
            Guid.NewGuid(),
            tierId: silver.Id,
            status: AffiliateStatus.Active,
            customCommissionRate: null);

        // With category commission (e.g. drinks = 11%) -> uses category rate
        var withCategory = AffiliateTierCalculator.ResolveEffectiveCommissionRate(
            regularAffiliate,
            silver,
            categoryCommissionPercentage: 11m);
        Assert.Equal(11m, withCategory.RatePercentage);
        Assert.Equal(EffectiveCommissionSource.CategoryRate, withCategory.Source);

        // Without category commission -> uses Silver tier rate (8%)
        var withoutCategory = AffiliateTierCalculator.ResolveEffectiveCommissionRate(
            regularAffiliate,
            silver,
            categoryCommissionPercentage: null);
        Assert.Equal(8m, withoutCategory.RatePercentage);
        Assert.Equal(EffectiveCommissionSource.TierRate, withoutCategory.Source);
    }

    [Fact]
    public void ComputeWalletCreditWithBonus_AppliesTierCreditBonusPercentage()
    {
        var gold = new AffiliateTier("طلایی", 25_000_000m, 12m, creditBonusPercentage: 20m);

        var totalCredit = AffiliateTierCalculator.ComputeWalletCreditWithBonus(1_000_000m, gold);

        Assert.Equal(1_200_000m, totalCredit);
        Assert.Equal(1_000_000m, AffiliateTierCalculator.ComputeWalletCreditWithBonus(1_000_000m, null));
    }
}
