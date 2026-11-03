namespace Sugarista.Domain.Entities;

/// <summary>What part of the cart a discount applies to.</summary>
public enum DiscountAppliesTo
{
    /// <summary>The whole order (items + tax base, before delivery).</summary>
    Order = 0,

    /// <summary>Only the lines matching the discount's item/category scope.</summary>
    EligibleItems = 1
}
