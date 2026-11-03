namespace Sugarista.Domain.Entities;

/// <summary>
/// The evaluation strategy of a discount. Each value is implemented by one
/// <c>IDiscountEvaluator</c> in the application layer, discovered by DI.
/// Adding a new kind of discount means adding a value here plus an evaluator —
/// no other part of the system (checkout, POS, admin, theme) has to change.
/// </summary>
public enum DiscountType
{
    /// <summary>
    /// Percentage or fixed amount off the whole order or only the eligible
    /// items. Codes, product/category offers, happy hour, birthday, group,
    /// online-payment and flash-sale discounts are all this type — they differ
    /// only in their conditions.
    /// </summary>
    AmountOff = 0,

    /// <summary>
    /// Cart-basket ladder: the discount depends on which spending tier the cart
    /// reached. Each tier defines its own threshold and percentage/fixed value.
    /// </summary>
    TieredCart = 1,

    /// <summary>
    /// Buy X eligible items, get Y eligible items for free. The free items are
    /// always the cheapest (or most expensive, if configured) eligible ones.
    /// </summary>
    BuyXGetY = 2
}
