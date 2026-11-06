using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

public enum CustomerGroupMembershipSource
{
    /// <summary>Added by an admin.</summary>
    Manual = 0,

    /// <summary>Auto-approved because the email matched the corporate domain.</summary>
    EmailDomain = 1
}

/// <summary>
/// One customer's membership in one customer group. Expiry is explicit so a
/// group can have a validity period (0 = evergreen group keeps it null).
/// </summary>
public class CustomerGroupMembership : BaseAuditableEntity
{
    public CustomerGroupMembership(
        Guid customerId,
        Guid groupId,
        CustomerGroupMembershipSource source,
        int validityDays)
    {
        if (customerId == Guid.Empty || groupId == Guid.Empty)
        {
            throw new DomainException("Customer group membership needs a customer and a group.");
        }

        CustomerId = customerId;
        GroupId = groupId;
        Source = source;
        StartedAt = DateTimeOffset.UtcNow;
        ExpiresAt = validityDays > 0 ? StartedAt.AddDays(validityDays) : null;
    }

    private CustomerGroupMembership()
    {
    }

    public Guid CustomerId { get; private set; }

    public Customer Customer { get; private set; } = null!;

    public Guid GroupId { get; private set; }

    public CustomerGroup Group { get; private set; } = null!;

    public CustomerGroupMembershipSource Source { get; private set; }

    public DateTimeOffset StartedAt { get; private set; }

    /// <summary>null = never expires.</summary>
    public DateTimeOffset? ExpiresAt { get; private set; }

    public bool IsActive(DateTimeOffset now) => ExpiresAt is null || ExpiresAt > now;

    /// <summary>Re-activates an existing row after a renewal/approval sweep.</summary>
    public void Renew(int validityDays)
    {
        StartedAt = DateTimeOffset.UtcNow;
        ExpiresAt = validityDays > 0 ? StartedAt.AddDays(validityDays) : null;
    }
}
