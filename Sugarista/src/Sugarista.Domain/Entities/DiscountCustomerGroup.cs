namespace Sugarista.Domain.Entities;

/// <summary>
/// Customer segment a discount is limited to. The segment is resolved by the
/// caller (checkout / POS) and handed to the engine in the evaluation context,
/// so the engine itself never has to query customer history.
/// </summary>
public enum DiscountCustomerGroup
{
    /// <summary>No restriction — every customer qualifies.</summary>
    All = 0,

    /// <summary>Customers without any previous paid order (first-order codes).</summary>
    New = 1,

    /// <summary>Customers who have ordered before (win-back codes).</summary>
    Returning = 2,

    Bronze = 3,
    Silver = 4,
    Gold = 5,

    Student = 6,
    Employee = 7,
    Corporate = 8
}
