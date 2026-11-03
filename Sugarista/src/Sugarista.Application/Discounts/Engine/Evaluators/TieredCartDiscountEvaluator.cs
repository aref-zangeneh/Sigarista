using Sugarista.Domain.Entities;

namespace Sugarista.Application.Discounts.Engine.Evaluators;

/// <summary>
/// Tiered cart discount: the cart subtotal picks one step of the ladder, and
/// that step's percentage or fixed amount is taken off the eligible base. The
/// admin defines the tiers; the engine handles scope, caps and stacking.
/// </summary>
public sealed class TieredCartDiscountEvaluator : IDiscountEvaluator
{
    public DiscountType Type => DiscountType.TieredCart;

    public DiscountComputation? Evaluate(
        Discount discount,
        DiscountEvaluationContext context,
        decimal eligibleBase,
        IReadOnlyList<DiscountLineInput> eligibleLines)
    {
        if (eligibleBase <= 0 || discount.Tiers.Count == 0)
        {
            return null;
        }

        var tier = DiscountTierSelector.SelectTier(discount, eligibleBase);
        if (tier is null)
        {
            return null;
        }

        var amount = DiscountTierSelector.AmountFor(tier, eligibleBase);
        if (amount <= 0)
        {
            return null;
        }

        var label = DiscountTierSelector.LabelFor(tier);

        return new DiscountComputation(
            amount,
            null,
            $"{discount.Name} — {label} (پله {tier.ThresholdAmount:N0})");
    }
}
