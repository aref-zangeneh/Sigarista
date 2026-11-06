using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

/// <summary>
/// A monthly subscription package (e.g. «۱۰ قهوه در ۳۰ روز»). The admin
/// defines how many uses the customer gets, the price, validity, the daily
/// cap and which product categories are covered.
/// </summary>
public class SubscriptionPackage : BaseAuditableEntity
{
    public SubscriptionPackage(
        string name,
        string? description,
        int totalUseCount,
        decimal price,
        int validityDays,
        int dailyUseLimit,
        string? allowedCategoryIds,
        int displayOrder = 0,
        bool isEnabled = true)
    {
        Update(name, description, totalUseCount, price, validityDays, dailyUseLimit, allowedCategoryIds, displayOrder, isEnabled);
    }

    private SubscriptionPackage()
    {
    }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    /// <summary>How many uses (units of eligible products) the package grants.</summary>
    public int TotalUseCount { get; private set; }

    /// <summary>Price the customer pays for the whole package (toman).</summary>
    public decimal Price { get; private set; }

    /// <summary>Days the subscription stays valid (must be positive).</summary>
    public int ValidityDays { get; private set; }

    /// <summary>Maximum uses per day (0 = no daily cap).</summary>
    public int DailyUseLimit { get; private set; }

    /// <summary>Comma-separated category ids the subscription covers (empty = all products).</summary>
    public string? AllowedCategoryIds { get; private set; }

    public int DisplayOrder { get; private set; }

    public bool IsEnabled { get; private set; }

    public void Update(
        string name,
        string? description,
        int totalUseCount,
        decimal price,
        int validityDays,
        int dailyUseLimit,
        string? allowedCategoryIds,
        int displayOrder,
        bool isEnabled)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Subscription package name is required.");
        }

        if (totalUseCount <= 0)
        {
            throw new DomainException("Subscription package use count must be positive.");
        }

        if (price < 0)
        {
            throw new DomainException("Subscription package price cannot be negative.");
        }

        if (validityDays <= 0)
        {
            throw new DomainException("Subscription package validity must be positive.");
        }

        if (dailyUseLimit < 0)
        {
            throw new DomainException("Subscription daily limit cannot be negative.");
        }

        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        TotalUseCount = totalUseCount;
        Price = price;
        ValidityDays = validityDays;
        DailyUseLimit = dailyUseLimit;
        AllowedCategoryIds = string.IsNullOrWhiteSpace(allowedCategoryIds)
            ? null
            : allowedCategoryIds.Trim();
        DisplayOrder = displayOrder;
        IsEnabled = isEnabled;
    }

    /// <summary>Price of one use — the basis for refunds of the remaining balance.</summary>
    public decimal PricePerUse => TotalUseCount > 0 ? Math.Round(Price / TotalUseCount, 2) : 0m;

    public IReadOnlyCollection<Guid> ParseCategoryIds()
    {
        if (string.IsNullOrWhiteSpace(AllowedCategoryIds))
        {
            return [];
        }

        return AllowedCategoryIds
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => Guid.TryParse(value, out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();
    }
}
