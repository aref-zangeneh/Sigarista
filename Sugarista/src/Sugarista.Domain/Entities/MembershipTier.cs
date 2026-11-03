using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

/// <summary>
/// One membership level of the customer club. Customers upgrade automatically
/// once their total paid spending inside the configured evaluation window
/// reaches <see cref="MinTotalPaidSpent"/>, and the level grants a standing
/// permanent discount applied automatically at checkout.
/// </summary>
public class MembershipTier : BaseAuditableEntity
{
    public MembershipTier(
        string name,
        string? description,
        decimal minTotalPaidSpent,
        decimal discountPercent,
        int displayOrder = 0,
        bool isEnabled = true)
    {
        Update(name, description, minTotalPaidSpent, discountPercent, displayOrder, isEnabled);
    }

    private MembershipTier()
    {
    }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    /// <summary>Total paid spending (toman) inside the evaluation window required for this level.</summary>
    public decimal MinTotalPaidSpent { get; private set; }

    /// <summary>Standing discount (0-100) granted while the customer holds this level.</summary>
    public decimal DiscountPercent { get; private set; }

    public int DisplayOrder { get; private set; }

    public bool IsEnabled { get; private set; }

    public void Update(
        string name,
        string? description,
        decimal minTotalPaidSpent,
        decimal discountPercent,
        int displayOrder,
        bool isEnabled)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Membership tier name is required.");
        }

        if (minTotalPaidSpent < 0)
        {
            throw new DomainException("Membership tier spending threshold cannot be negative.");
        }

        if (discountPercent is < 0 or > 100)
        {
            throw new DomainException("Membership tier discount must be between 0 and 100.");
        }

        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        MinTotalPaidSpent = minTotalPaidSpent;
        DiscountPercent = discountPercent;
        DisplayOrder = displayOrder;
        IsEnabled = isEnabled;
    }
}
