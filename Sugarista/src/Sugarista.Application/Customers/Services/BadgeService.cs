using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Customers.Services;

/// <summary>
/// Automatic badge grants: whenever a customer reaches the conditions an
/// admin defined on a badge (paid orders count and/or total paid spending),
/// the badge is granted automatically. Re-running the evaluation is safe —
/// a badge is only granted once per customer.
/// </summary>
public static class BadgeService
{
    public static async Task EvaluateForCustomerAsync(
        IApplicationDbContext dbContext,
        Customer customer,
        CancellationToken cancellationToken = default)
    {
        var badges = await dbContext.Badges
            .AsNoTracking()
            .Where(entry => entry.IsEnabled)
            .OrderBy(entry => entry.DisplayOrder)
            .ToListAsync(cancellationToken);

        if (badges.Count == 0)
        {
            return;
        }

        var ownedBadgeIds = await dbContext.CustomerBadges
            .Where(award => award.CustomerId == customer.Id)
            .Select(award => award.BadgeId)
            .ToListAsync(cancellationToken);
        var owned = ownedBadgeIds.ToHashSet();

        var earned = await dbContext.Orders
            .Where(orderEntry => orderEntry.CustomerId == customer.Id && orderEntry.Status == OrderStatus.Paid)
            .GroupBy(_ => true)
            .Select(group => new { Count = group.Count(), Total = group.Sum(orderEntry => orderEntry.TotalAmount) })
            .FirstOrDefaultAsync(cancellationToken);

        var paidOrders = earned?.Count ?? 0;
        var totalSpent = earned?.Total ?? 0m;

        var changed = false;
        foreach (var badge in badges)
        {
            if (owned.Contains(badge.Id) || !badge.Matches(paidOrders, totalSpent))
            {
                continue;
            }

            dbContext.CustomerBadges.Add(new CustomerBadge(customer.Id, badge.Id));
            changed = true;
        }

        if (changed)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
