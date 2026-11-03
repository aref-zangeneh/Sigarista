namespace Sugarista.Domain.Entities;

/// <summary>How the <see cref="Discount.Value"/> is interpreted.</summary>
public enum DiscountValueKind
{
    /// <summary>Value is a percentage (0-100) of the eligible amount.</summary>
    Percentage = 0,

    /// <summary>Value is a fixed amount of money.</summary>
    FixedAmount = 1
}
