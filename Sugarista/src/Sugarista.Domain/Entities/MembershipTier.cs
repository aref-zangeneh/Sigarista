using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

/// <summary>
/// One membership level of the customer club. Customers qualify automatically
/// when their paid activity inside the configured evaluation window reaches
/// either the spending threshold or the paid-orders threshold (each optional),
/// and the level grants a standing permanent discount as well as — optionally —
/// its own cashback percent that overrides the global one.
/// </summary>
public class MembershipTier : BaseAuditableEntity
{
    public MembershipTier(
        string name,
        string? description,
        decimal minTotalPaidSpent,
        decimal discountPercent,
        int displayOrder = 0,
        bool isEnabled = true,
        int minPaidOrders = 0,
        decimal? cashbackPercent = null)
    {
        Update(name, description, minTotalPaidSpent, discountPercent, displayOrder, isEnabled, minPaidOrders, cashbackPercent);
    }

    private MembershipTier()
    {
    }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    /// <summary>Total paid spending (toman) inside the evaluation window required for this level (0 = no spending requirement).</summary>
    public decimal MinTotalPaidSpent { get; private set; }

    /// <summary>Paid orders inside the evaluation window required for this level (0 = no order-count requirement).</summary>
    public int MinPaidOrders { get; private set; }

    /// <summary>Standing discount (0-100) granted while the customer holds this level.</summary>
    public decimal DiscountPercent { get; private set; }

    /// <summary>
    /// Tier-specific cashback percent (0-100). Null falls back to the global
    /// cashback percent; a value (including 0) overrides it per level.
    /// </summary>
    public decimal? CashbackPercent { get; private set; }

    public int DisplayOrder { get; private set; }

    public bool IsEnabled { get; private set; }

    public void Update(
        string name,
        string? description,
        decimal minTotalPaidSpent,
        decimal discountPercent,
        int displayOrder,
        bool isEnabled,
        int minPaidOrders = 0,
        decimal? cashbackPercent = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Membership tier name is required.");
        }

        if (minTotalPaidSpent < 0)
        {
            throw new DomainException("Membership tier spending threshold cannot be negative.");
        }

        if (minPaidOrders < 0)
        {
            throw new DomainException("Membership tier order threshold cannot be negative.");
        }

        if (discountPercent is < 0 or > 100)
        {
            throw new DomainException("Membership tier discount must be between 0 and 100.");
        }

        if (cashbackPercent is < 0 or > 100)
        {
            throw new DomainException("Membership tier cashback percent must be between 0 and 100.");
        }

        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        MinTotalPaidSpent = minTotalPaidSpent;
        MinPaidOrders = minPaidOrders;
        DiscountPercent = discountPercent;
        CashbackPercent = cashbackPercent;
        DisplayOrder = displayOrder;
        IsEnabled = isEnabled;
    }
}
