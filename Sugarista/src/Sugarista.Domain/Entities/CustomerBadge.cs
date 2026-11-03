using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

/// <summary>
/// One earned badge on a customer profile: granted automatically when the
/// customer first satisfies the badge earning conditions.
/// </summary>
public class CustomerBadge : BaseAuditableEntity
{
    private CustomerBadge()
    {
    }

    public CustomerBadge(Guid customerId, Guid badgeId)
    {
        if (customerId == Guid.Empty)
        {
            throw new DomainException("Customer id is required.");
        }

        if (badgeId == Guid.Empty)
        {
            throw new DomainException("Badge id is required.");
        }

        CustomerId = customerId;
        BadgeId = badgeId;
    }

    public Guid CustomerId { get; private set; }

    public Customer Customer { get; private set; } = null!;

    public Guid BadgeId { get; private set; }

    public Badge Badge { get; private set; } = null!;
}
