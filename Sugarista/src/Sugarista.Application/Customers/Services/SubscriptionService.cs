using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Customers.Services;

/// <summary>
/// Subscription mechanics shared by checkout, POS and the customer profile:
/// resolve the active subscription, mark expired ones, and compute how many
/// eligible units an order can redeem (cheapest eligible units first, bounded
/// by the remaining count and the daily cap).
/// </summary>
public static class SubscriptionService
{
    public static async Task<CustomerSubscription?> GetActiveAsync(
        IApplicationDbContext dbContext,
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var subscription = await dbContext.CustomerSubscriptions
            .Where(entry => entry.CustomerId == customerId &&
                            entry.Status == CustomerSubscriptionStatus.Active)
            .OrderByDescending(entry => entry.ExpiresAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (subscription is null)
        {
            return null;
        }

        if (subscription.ExpiresAt <= now)
        {
            subscription.MarkExpired();
            await dbContext.SaveChangesAsync(cancellationToken);
            return null;
        }

        return subscription.IsUsable(now) ? subscription : null;
    }

    /// <summary>
    /// Redeemable units of the given order lines. Lines outside the covered
    /// categories are ignored; the cheapest eligible units are consumed first
    /// (standard practice, keeps the benefit predictable for the customer).
    /// </summary>
    public static (IReadOnlyCollection<(Guid MenuItemId, int Quantity, decimal UnitPrice)> Units, int TotalUnits)
        PlanRedemption(CustomerSubscription subscription, IEnumerable<(Guid MenuItemId, int Quantity, decimal UnitPrice, IReadOnlyCollection<Guid> CategoryIds)> lines)
    {
        var allowed = ParseCategoryIds(subscription.AllowedCategoryIds);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var available = subscription.AvailableToday(today);

        if (available <= 0)
        {
            return ([], 0);
        }

        var eligibleUnits = lines
            .Where(line => allowed.Count == 0 || line.CategoryIds.Any(allowed.Contains))
            .SelectMany(line => Enumerable.Repeat(line.UnitPrice, Math.Max(0, line.Quantity))
                .Select(price => (line.MenuItemId, price)))
            .OrderBy(unit => unit.price)
            .ToList();

        var take = Math.Min(available, eligibleUnits.Count);
        if (take == 0)
        {
            return ([], 0);
        }

        var grouped = eligibleUnits
            .Take(take)
            .GroupBy(unit => unit.MenuItemId)
            .Select(group => (group.Key, group.Count(), group.First().price))
            .ToList();

        return (grouped, take);
    }

    public static IReadOnlyCollection<Guid> ParseCategoryIds(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        return raw
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => Guid.TryParse(value, out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();
    }
}
