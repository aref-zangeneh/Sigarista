using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

/// <summary>
/// A completed stamp card for a customer: earned automatically once the
/// customer reaches the paid-order threshold in <see cref="SiteSettings"/>.
/// Holds the reward as a percent discount that can be redeemed at checkout.
/// </summary>
public class StampCardReward : BaseAuditableEntity
{
    private StampCardReward()
    {
    }

    public StampCardReward(
        Guid customerId,
        int fullCardNumber,
        string code,
        decimal rewardPercent,
        DateTimeOffset? expiresAt)
    {
        if (fullCardNumber <= 0)
        {
            throw new DomainException("Stamp card number must be positive.");
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            throw new DomainException("Stamp card reward code is required.");
        }

        if (rewardPercent is <= 0 or > 100)
        {
            throw new DomainException("Stamp card reward percent must be between 1 and 100.");
        }

        CustomerId = customerId;
        FullCardNumber = fullCardNumber;
        Code = code.Trim().ToUpperInvariant();
        RewardPercent = rewardPercent;
        ExpiresAt = expiresAt;
    }

    public Guid CustomerId { get; private set; }

    public Customer Customer { get; private set; } = null!;

    /// <summary>1 = first completed card, 2 = second, and so on.</summary>
    public int FullCardNumber { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public decimal RewardPercent { get; private set; }

    public bool IsRedeemed { get; private set; }

    public DateTimeOffset? RedeemedAt { get; private set; }

    public Guid? RedeemedOrderId { get; private set; }

    public DateTimeOffset? ExpiresAt { get; private set; }

    public bool IsUsable(DateTimeOffset now) => !IsRedeemed && (!ExpiresAt.HasValue || ExpiresAt.Value > now);

    public void MarkRedeemed(DateTimeOffset now, Guid orderId)
    {
        IsRedeemed = true;
        RedeemedAt = now;
        RedeemedOrderId = orderId;
    }
}
