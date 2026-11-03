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

        var bestTierId = await dbContext.MembershipTiers
            .AsNoTracking()
            .Where(tier => tier.IsEnabled &&
                           tier.MinTotalPaidSpent <= windowSpent)
            .OrderByDescending(tier => tier.MinTotalPaidSpent)
            .Select(tier => (Guid?)tier.Id)
            .FirstOrDefaultAsync(cancellationToken);

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
