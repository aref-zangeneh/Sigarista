using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Customers.Services;

/// <summary>
/// Customer-group benefits: an active membership of an enabled group grants
/// its standing discount, and domain-approved groups auto-approve customers
/// whose email address belongs to the corporate domain.
/// </summary>
public static class CustomerGroupService
{
    /// <summary>Best active group membership of the customer (highest discount wins).</summary>
    public static async Task<(CustomerGroup Group, CustomerGroupMembership Membership)?> GetActiveMembershipAsync(
        IApplicationDbContext dbContext,
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var rows = await dbContext.CustomerGroupMemberships
            .AsNoTracking()
            .Include(membership => membership.Group)
            .Where(membership => membership.CustomerId == customerId &&
                                 membership.Group.IsEnabled &&
                                 (membership.ExpiresAt == null || membership.ExpiresAt > now))
            .ToListAsync(cancellationToken);

        var best = rows
            .OrderByDescending(membership => membership.Group.DiscountPercent)
            .FirstOrDefault();

        return best is null ? null : (best.Group, best);
    }

    /// <summary>
    /// Domain-approval sweep: every customer whose email domain matches an
    /// enabled domain-approved group is added (or renewed) as a member.
    /// Returns how many memberships were created/refreshed.
    /// </summary>
    public static async Task<int> SweepEmailDomainMembershipsAsync(
        IApplicationDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        var domainGroups = await dbContext.CustomerGroups
            .Where(group => group.IsEnabled &&
                            group.ApprovalMethod == CustomerGroupApprovalMethod.CorporateEmailDomain &&
                            group.EmailDomain != null)
            .ToListAsync(cancellationToken);

        if (domainGroups.Count == 0)
        {
            return 0;
        }

        var customers = await dbContext.Customers
            .Where(customer => customer.Email != null && customer.Email != string.Empty)
            .ToListAsync(cancellationToken);

        var touched = 0;
        foreach (var group in domainGroups)
        {
            var domain = group.EmailDomain!;
            var matching = customers.Where(customer =>
                (customer.Email ?? string.Empty).Trim().ToLowerInvariant().EndsWith("@" + domain, StringComparison.Ordinal));

            foreach (var customer in matching)
            {
                var membership = await dbContext.CustomerGroupMemberships
                    .FirstOrDefaultAsync(
                        entry => entry.CustomerId == customer.Id && entry.GroupId == group.Id,
                        cancellationToken);

                if (membership is null)
                {
                    dbContext.CustomerGroupMemberships.Add(new CustomerGroupMembership(
                        customer.Id,
                        group.Id,
                        CustomerGroupMembershipSource.EmailDomain,
                        group.MembershipValidityDays));
                    touched++;
                }
                else if (!membership.IsActive(DateTimeOffset.UtcNow))
                {
                    membership.Renew(group.MembershipValidityDays);
                    touched++;
                }
            }
        }

        if (touched > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return touched;
    }

    /// <summary>
    /// Per-customer domain approval: called whenever a customer's email changes
    /// so a corporate address is approved (and a stale domain membership is
    /// revoked) without waiting for the next full sweep.
    /// </summary>
    public static async Task ApplyEmailDomainMembershipsAsync(
        IApplicationDbContext dbContext,
        Customer customer,
        CancellationToken cancellationToken = default)
    {
        var domainGroups = await dbContext.CustomerGroups
            .Where(group => group.IsEnabled &&
                            group.ApprovalMethod == CustomerGroupApprovalMethod.CorporateEmailDomain &&
                            group.EmailDomain != null)
            .ToListAsync(cancellationToken);

        var email = (customer.Email ?? string.Empty).Trim().ToLowerInvariant();

        foreach (var group in domainGroups)
        {
            var matches = email.Length > 0 &&
                          email.EndsWith("@" + group.EmailDomain!, StringComparison.Ordinal);
            var membership = await dbContext.CustomerGroupMemberships
                .FirstOrDefaultAsync(
                    entry => entry.CustomerId == customer.Id && entry.GroupId == group.Id,
                    cancellationToken);

            if (matches)
            {
                if (membership is null)
                {
                    dbContext.CustomerGroupMemberships.Add(new CustomerGroupMembership(
                        customer.Id,
                        group.Id,
                        CustomerGroupMembershipSource.EmailDomain,
                        group.MembershipValidityDays));
                }
                else if (membership.Source == CustomerGroupMembershipSource.EmailDomain &&
                         !membership.IsActive(DateTimeOffset.UtcNow))
                {
                    membership.Renew(group.MembershipValidityDays);
                }
            }
            else if (membership is { Source: CustomerGroupMembershipSource.EmailDomain })
            {
                // The email no longer belongs to the corporate domain: the
                // automatic approval is withdrawn (manual memberships stay).
                dbContext.CustomerGroupMemberships.Remove(membership);
            }
        }
    }

    /// <summary>
    /// Adds the customer to a group manually (or renews an existing
    /// membership) — the admin-side approval path.
    /// </summary>
    public static async Task<CustomerGroupMembership> AssignAsync(
        IApplicationDbContext dbContext,
        CustomerGroup group,
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        var membership = await dbContext.CustomerGroupMemberships
            .FirstOrDefaultAsync(
                entry => entry.CustomerId == customerId && entry.GroupId == group.Id,
                cancellationToken);

        if (membership is null)
        {
            membership = new CustomerGroupMembership(
                customerId,
                group.Id,
                CustomerGroupMembershipSource.Manual,
                group.MembershipValidityDays);
            dbContext.CustomerGroupMemberships.Add(membership);
        }
        else
        {
            membership.Renew(group.MembershipValidityDays);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return membership;
    }

    /// <summary>Removes a membership (admin decision).</summary>
    public static async Task<bool> RemoveAsync(
        IApplicationDbContext dbContext,
        Guid customerId,
        Guid groupId,
        CancellationToken cancellationToken = default)
    {
        var membership = await dbContext.CustomerGroupMemberships
            .FirstOrDefaultAsync(
                entry => entry.CustomerId == customerId && entry.GroupId == groupId,
                cancellationToken);

        if (membership is null)
        {
            return false;
        }

        dbContext.CustomerGroupMemberships.Remove(membership);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
