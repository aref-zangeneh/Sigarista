using Sugarista.Domain.Entities;

namespace Sugarista.Application.Discounts.Engine;

/// <summary>
/// Picks the tier that applies to a cart subtotal. Shared by the checkout
/// evaluator and the storefront progress query so both always agree on which
/// step of the ladder the cart is on.
/// </summary>
public static class DiscountTierSelector
{
    /// <summary>The highest tier whose threshold the subtotal reaches, or null.</summary>
    public static DiscountTier? SelectTier(Discount discount, decimal subtotal)
        => discount.Tiers
            .Where(tier => tier.ThresholdAmount <= subtotal)
            .OrderByDescending(tier => tier.ThresholdAmount)
            .FirstOrDefault();

    /// <summary>The nearest tier above the subtotal, or null when the top tier is reached.</summary>
    public static DiscountTier? SelectNextTier(Discount discount, decimal subtotal)
        => discount.Tiers
            .Where(tier => tier.ThresholdAmount > subtotal)
            .OrderBy(tier => tier.ThresholdAmount)
            .FirstOrDefault();

    /// <summary>Money a tier takes off a given base, capped to the base itself.</summary>
    public static decimal AmountFor(DiscountTier tier, decimal baseAmount)
    {
        var amount = tier.ValueKind == DiscountValueKind.Percentage
            ? Math.Round(baseAmount * tier.Value / 100m, 2, MidpointRounding.AwayFromZero)
            : tier.Value;

        return Math.Min(Math.Max(0, amount), Math.Max(0, baseAmount));
    }

    public static string LabelFor(DiscountTier tier)
        => tier.ValueKind == DiscountValueKind.Percentage
            ? $"{tier.Value:0.##}٪ تخفیف"
            : "تخفیف نقدی";
}
