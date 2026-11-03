using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

/// <summary>
/// An admin-defined loyalty badge: customers earn it automatically once
/// their lifetime paid orders reach <see cref="MinPaidOrders"/> (count) or
/// their total paid spending reaches <see cref="MinTotalSpent"/>.
/// At least one condition must be defined; a null condition is not evaluated.
/// </summary>
public class Badge : BaseAuditableEntity
{
    public Badge(
        string name,
        string? description,
        string? imageUrl,
        int? minPaidOrders,
        decimal? minTotalSpent,
        bool isEnabled = true,
        int displayOrder = 0)
    {
        Update(name, description, imageUrl, minPaidOrders, minTotalSpent, isEnabled, displayOrder);
    }

    private Badge()
    {
    }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    /// <summary>Uploaded badge artwork (svg/png/jpeg). Null falls back to a default icon.</summary>
    public string? ImageUrl { get; private set; }

    /// <summary>Minimum lifetime paid orders (null = not evaluated).</summary>
    public int? MinPaidOrders { get; private set; }

    /// <summary>Minimum lifetime paid spending in toman (null = not evaluated).</summary>
    public decimal? MinTotalSpent { get; private set; }

    public bool IsEnabled { get; private set; }

    public int DisplayOrder { get; private set; }

    public ICollection<CustomerBadge> Awards { get; private set; } = [];

    public void Update(
        string name,
        string? description,
        string? imageUrl,
        int? minPaidOrders,
        decimal? minTotalSpent,
        bool isEnabled,
        int displayOrder)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Badge name is required.");
        }

        if (minPaidOrders is null && minTotalSpent is null)
        {
            throw new DomainException("A badge needs at least one earning condition.");
        }

        if (minPaidOrders is < 0)
        {
            throw new DomainException("Badge minimum paid orders cannot be negative.");
        }

        if (minTotalSpent is < 0)
        {
            throw new DomainException("Badge minimum total spent cannot be negative.");
        }

        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        ImageUrl = string.IsNullOrWhiteSpace(imageUrl) ? null : imageUrl.Trim();
        MinPaidOrders = minPaidOrders;
        MinTotalSpent = minTotalSpent;
        IsEnabled = isEnabled;
        DisplayOrder = displayOrder;
    }

    public void SetImage(string? imageUrl)
    {
        ImageUrl = string.IsNullOrWhiteSpace(imageUrl) ? null : imageUrl.Trim();
    }

    public bool Matches(int paidOrders, decimal totalSpent)
    {
        if (MinPaidOrders.HasValue && paidOrders < MinPaidOrders.Value)
        {
            return false;
        }

        if (MinTotalSpent.HasValue && totalSpent < MinTotalSpent.Value)
        {
            return false;
        }

        return MinPaidOrders.HasValue || MinTotalSpent.HasValue;
    }
}
