using Sugarista.Domain.Entities;

namespace Sugarista.Application.Discounts.Engine.Evaluators;

/// <summary>
/// Percentage or fixed amount off the eligible amount. Covers every "take X off"
/// offer: promo codes, product/category discounts, happy hour, birthday,
/// customer-group and online-payment discounts — they only differ by the
/// conditions the engine already enforces.
/// </summary>
public sealed class AmountOffDiscountEvaluator : IDiscountEvaluator
{
    public DiscountType Type => DiscountType.AmountOff;

    public DiscountComputation? Evaluate(
        Discount discount,
        DiscountEvaluationContext context,
        decimal eligibleBase,
        IReadOnlyList<DiscountLineInput> eligibleLines)
    {
        if (eligibleBase <= 0)
        {
            return null;
        }

        var amount = discount.ValueKind == DiscountValueKind.Percentage
            ? Math.Round(eligibleBase * discount.Value / 100m, 2, MidpointRounding.AwayFromZero)
            : discount.Value;

        if (discount.MaxDiscountAmount is { } cap)
        {
            amount = Math.Min(amount, cap);
        }

        amount = Math.Min(amount, eligibleBase);

        if (amount <= 0)
        {
            return null;
        }

        var label = discount.ValueKind == DiscountValueKind.Percentage
            ? $"{discount.Value:0.##}٪ تخفیف"
            : "تخفیف نقدی";

        return new DiscountComputation(amount, null, $"{discount.Name} — {label}");
    }
}
