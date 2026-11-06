using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Customers.Services;

/// <summary>
/// Membership level auto-upgrades: the customer's total paid spending inside
/// the rolling evaluation window decides the highest enabled level they hold,
/// and that level (its name and standing permanent discount) is applied
/// automatically — at payment time and whenever the profile is viewed.
/// </summary>
public static class MembershipTierService
{
    public static async Task EvaluateForCustomerAsync(
        IApplicationDbContext dbContext,
        Customer customer,
        CancellationToken cancellationToken = default)
    {
        var settings = await dbContext.SiteSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        if (settings is { MembershipTiersEnabled: false })
        {
            return;
        }

        var windowDays = settings?.MembershipWindowDays ?? 365;
        var windowStart = windowDays > 0
            ? DateTimeOffset.UtcNow.AddDays(-windowDays)
            : DateTimeOffset.MinValue;

        var windowSpent = await dbContext.Orders
            .Where(orderEntry => orderEntry.CustomerId == customer.Id &&
                                 orderEntry.Status == OrderStatus.Paid &&
                                 orderEntry.CreatedAt >= windowStart)
            .SumAsync(orderEntry => (decimal?)orderEntry.TotalAmount, cancellationToken) ?? 0m;

        var windowPaidOrders = await dbContext.Orders
            .Where(orderEntry => orderEntry.CustomerId == customer.Id &&
                                 orderEntry.Status == OrderStatus.Paid &&
                                 orderEntry.CreatedAt >= windowStart)
            .CountAsync(cancellationToken);

        var candidates = await dbContext.MembershipTiers
            .AsNoTracking()
            .Where(tier => tier.IsEnabled)
            .OrderByDescending(tier => tier.MinTotalPaidSpent)
            .ThenByDescending(tier => tier.MinPaidOrders)
            .ThenBy(tier => tier.DisplayOrder)
            .ToListAsync(cancellationToken);

        // Upgrade condition: reach the spending OR the paid-orders threshold
        // inside the window (each level defines them optionally).
        static bool Qualifies(Sugarista.Domain.Entities.MembershipTier tier, decimal spent, int paidOrders) =>
            (tier.MinTotalPaidSpent > 0 && spent >= tier.MinTotalPaidSpent) ||
            (tier.MinPaidOrders > 0 && paidOrders >= tier.MinPaidOrders);

        var bestTierId = candidates.FirstOrDefault(tier => Qualifies(tier, windowSpent, windowPaidOrders))?.Id;

        // قوانین تنزل: evaluation runs on every paid order/profil view, so a
        // customer who falls below their level's thresholds inside the window
        // is demoted — unless the admin switched demotion off.
        var demote = settings is null || settings.MembershipDowngradeEnabled;
        if (!demote && bestTierId is null)
        {
            return;
        }
        if (!demote && customer.MembershipTierId is not null)
        {
            // Levels only ever go up: keep the better of current vs candidate.
            var currentRank = candidates.FindIndex(tier => tier.Id == customer.MembershipTierId);
            var bestRank = candidates.FindIndex(tier => tier.Id == bestTierId);
            if (currentRank >= 0 && (bestRank < 0 || currentRank <= bestRank))
            {
                return;
            }
        }

        if (customer.MembershipTierId == bestTierId)
        {
            return;
        }

        customer.SetMembershipTier(bestTierId);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Window spending for one customer — reused by the profile query.</summary>
    public static async Task<decimal> GetWindowSpentAsync(
        IApplicationDbContext dbContext,
        Guid customerId,
        int windowDays,
        CancellationToken cancellationToken = default)
    {
        var windowStart = windowDays > 0
            ? DateTimeOffset.UtcNow.AddDays(-windowDays)
            : DateTimeOffset.MinValue;

        return await dbContext.Orders
            .Where(orderEntry => orderEntry.CustomerId == customerId &&
                                 orderEntry.Status == OrderStatus.Paid &&
                                 orderEntry.CreatedAt >= windowStart)
            .SumAsync(orderEntry => (decimal?)orderEntry.TotalAmount, cancellationToken) ?? 0m;
    }
}
