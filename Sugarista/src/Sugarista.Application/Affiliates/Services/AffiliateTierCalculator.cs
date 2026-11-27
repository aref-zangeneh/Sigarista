using Sugarista.Domain.Entities;

namespace Sugarista.Application.Affiliates.Services;

public enum EffectiveCommissionSource
{
    DefaultZero = 0,
    TierRate = 1,
    CategoryRate = 2,
    CustomVipOverride = 3
}

public sealed record EffectiveCommissionRateResult(
    decimal RatePercentage,
    EffectiveCommissionSource Source);

/// <summary>
/// Pure, deterministic calculation rules for the Affiliate Tier Engine:
/// tier matching (upgrade & downgrade), VIP custom commission rate override,
/// category commission resolution, and cafe-credit bonus computation.
/// </summary>
public static class AffiliateTierCalculator
{
    public const int RollingWindowDays = 30;

    /// <summary>
    /// Resolves the highest tier whose MinMonthlySales is met by the affiliate's
    /// rolling 30-day approved sales. Handles both upgrades and downgrades.
    /// </summary>
    public static AffiliateTier? ResolveTierForSales(
        decimal rolling30DayApprovedSales,
        IEnumerable<AffiliateTier> tiers)
    {
        var normalizedSales = Math.Max(0m, rolling30DayApprovedSales);

        return tiers
            .Where(t => t.MinMonthlySales <= normalizedSales)
            .OrderByDescending(t => t.MinMonthlySales)
            .ThenByDescending(t => t.CommissionPercentage)
            .FirstOrDefault();
    }

    /// <summary>
    /// Resolves the effective commission rate for an affiliate:
    /// 1. VIP CustomCommissionRate (always overrides progressive tier & category rules)
    /// 2. CategoryCommission percentage (when provided)
    /// 3. Assigned Tier CommissionPercentage
    /// 4. Fallback 0%
    /// </summary>
    public static EffectiveCommissionRateResult ResolveEffectiveCommissionRate(
        Affiliate affiliate,
        AffiliateTier? assignedTier,
        decimal? categoryCommissionPercentage = null)
    {
        if (affiliate.CustomCommissionRate.HasValue)
        {
            return new EffectiveCommissionRateResult(
                affiliate.CustomCommissionRate.Value,
                EffectiveCommissionSource.CustomVipOverride);
        }

        if (categoryCommissionPercentage.HasValue)
        {
            return new EffectiveCommissionRateResult(
                categoryCommissionPercentage.Value,
                EffectiveCommissionSource.CategoryRate);
        }

        if (assignedTier is not null)
        {
            return new EffectiveCommissionRateResult(
                assignedTier.CommissionPercentage,
                EffectiveCommissionSource.TierRate);
        }

        return new EffectiveCommissionRateResult(0m, EffectiveCommissionSource.DefaultZero);
    }

    /// <summary>
    /// Computes total cafe wallet credit when an affiliate converts payout to
    /// WalletCredit, adding the tier's CreditBonusPercentage if configured.
    /// </summary>
    public static decimal ComputeWalletCreditWithBonus(decimal baseAmount, AffiliateTier? assignedTier)
    {
        if (baseAmount <= 0m)
        {
            return 0m;
        }

        var bonusPct = assignedTier?.CreditBonusPercentage ?? 0m;
        if (bonusPct <= 0m)
        {
            return baseAmount;
        }

        var bonus = Math.Floor(baseAmount * bonusPct / 100m);
        return baseAmount + bonus;
    }
}
