using Sugarista.Domain.Entities;

namespace Sugarista.Application.Affiliates.Services;

public enum AffiliateAttributionSource
{
    None = 0,
    Coupon = 1,
    Cookie = 2
}

public sealed record AffiliateAttributionResult(
    Guid? AffiliateId,
    AffiliateAttributionSource Source);

public sealed record AffiliateCommissionLineInput(
    Guid MenuItemId,
    Guid CategoryId,
    decimal LineSubtotal);

public sealed record AffiliateCommissionLineBreakdown(
    Guid MenuItemId,
    Guid CategoryId,
    decimal LineSubtotal,
    decimal LineDiscountShare,
    decimal NetEligibleAmount,
    decimal AppliedRatePercentage,
    EffectiveCommissionSource RateSource,
    decimal CommissionAmount);

public sealed record AffiliateCommissionCalculationResult(
    decimal EligibleAmount,
    decimal CommissionAmount,
    IReadOnlyList<AffiliateCommissionLineBreakdown> Lines);

/// <summary>
/// Pure, deterministic calculations for Phase 2 Hybrid Tracking:
/// attribution priority (Coupon > Cookie), coupon discount math, and
/// net category-based commission calculation (excluding tax & delivery fee).
/// </summary>
public static class AffiliateCommissionCalculator
{
    public const int DefaultCookieDays = 14;

    /// <summary>
    /// Priority 1: Affiliate Coupon always wins over browser cookie.
    /// Priority 2: Valid aff_token cookie (Last-Click Attribution).
    /// </summary>
    public static AffiliateAttributionResult ResolveAttribution(
        Guid? couponAffiliateId,
        Guid? cookieAffiliateId)
    {
        if (couponAffiliateId.HasValue && couponAffiliateId.Value != Guid.Empty)
        {
            return new AffiliateAttributionResult(couponAffiliateId.Value, AffiliateAttributionSource.Coupon);
        }

        if (cookieAffiliateId.HasValue && cookieAffiliateId.Value != Guid.Empty)
        {
            return new AffiliateAttributionResult(cookieAffiliateId.Value, AffiliateAttributionSource.Cookie);
        }

        return new AffiliateAttributionResult(null, AffiliateAttributionSource.None);
    }

    public static decimal CalculateCouponDiscount(AffiliateCoupon coupon, decimal itemsSubtotal)
    {
        if (itemsSubtotal <= 0m || !coupon.IsActive)
        {
            return 0m;
        }

        decimal rawDiscount = coupon.DiscountType switch
        {
            AffiliateDiscountType.Percentage => Math.Floor(itemsSubtotal * coupon.DiscountValue / 100m),
            AffiliateDiscountType.FixedAmount => coupon.DiscountValue,
            _ => 0m,
        };

        if (coupon.MaxDiscountAmount.HasValue && rawDiscount > coupon.MaxDiscountAmount.Value)
        {
            rawDiscount = coupon.MaxDiscountAmount.Value;
        }

        return Math.Clamp(rawDiscount, 0m, itemsSubtotal);
    }

    /// <summary>
    /// Computes commission strictly from net item subtotal after discounts
    /// (excluding tax and delivery fees), applying each item's CategoryCommission
    /// rate (or VIP CustomCommissionRate override / Tier rate).
    /// </summary>
    public static AffiliateCommissionCalculationResult CalculateOrderCommission(
        Affiliate affiliate,
        AffiliateTier? assignedTier,
        IReadOnlyList<AffiliateCommissionLineInput> lines,
        decimal totalDiscountAmount,
        IReadOnlyDictionary<Guid, decimal> categoryCommissionRates)
    {
        if (lines.Count == 0)
        {
            return new AffiliateCommissionCalculationResult(0m, 0m, []);
        }

        var grossItemsSubtotal = lines.Sum(l => Math.Max(0m, l.LineSubtotal));
        if (grossItemsSubtotal <= 0m)
        {
            return new AffiliateCommissionCalculationResult(0m, 0m, []);
        }

        var effectiveDiscount = Math.Clamp(totalDiscountAmount, 0m, grossItemsSubtotal);
        var breakdowns = new List<AffiliateCommissionLineBreakdown>(lines.Count);

        decimal remainingDiscount = effectiveDiscount;
        decimal totalEligible = 0m;
        decimal totalCommission = 0m;

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var lineSubtotal = Math.Max(0m, line.LineSubtotal);

            decimal lineDiscountShare;
            if (i == lines.Count - 1)
            {
                lineDiscountShare = Math.Min(lineSubtotal, remainingDiscount);
            }
            else
            {
                lineDiscountShare = Math.Round(effectiveDiscount * (lineSubtotal / grossItemsSubtotal), 2);
                lineDiscountShare = Math.Min(lineSubtotal, Math.Min(lineDiscountShare, remainingDiscount));
            }

            remainingDiscount -= lineDiscountShare;
            var netEligible = Math.Max(0m, lineSubtotal - lineDiscountShare);

            decimal? categoryRate = categoryCommissionRates.TryGetValue(line.CategoryId, out var catPct)
                ? catPct
                : null;

            var rateResult = AffiliateTierCalculator.ResolveEffectiveCommissionRate(
                affiliate,
                assignedTier,
                categoryRate);

            var lineCommission = Math.Floor(netEligible * rateResult.RatePercentage / 100m);

            totalEligible += netEligible;
            totalCommission += lineCommission;

            breakdowns.Add(new AffiliateCommissionLineBreakdown(
                line.MenuItemId,
                line.CategoryId,
                lineSubtotal,
                lineDiscountShare,
                netEligible,
                rateResult.RatePercentage,
                rateResult.Source,
                lineCommission));
        }

        return new AffiliateCommissionCalculationResult(
            Math.Round(totalEligible, 2),
            totalCommission,
            breakdowns);
    }
}
