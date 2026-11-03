using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

/// <summary>
/// One movement on a customer's loyalty balance: earning from a paid order,
/// redeeming for a discount, expiry, or a refund after cancellation.
/// </summary>
public class LoyaltyTransaction : BaseAuditableEntity
{
    private LoyaltyTransaction()
    {
    }

    public LoyaltyTransaction(
        Guid customerId,
        LoyaltyTransactionType type,
        decimal points,
        decimal balanceAfter,
        string description,
        Guid? orderId = null,
        DateTimeOffset? expiresAt = null)
    {
        if (points == 0)
        {
            throw new DomainException("Loyalty transaction points cannot be zero.");
        }

        if (balanceAfter < 0)
        {
            throw new DomainException("Loyalty balance cannot be negative.");
        }

        CustomerId = customerId;
        Type = type;
        Points = points;
        BalanceAfter = balanceAfter;
        Description = string.IsNullOrWhiteSpace(description) ? type.ToString() : description.Trim();
        OrderId = orderId;
        ExpiresAt = expiresAt;
    }

    public Guid CustomerId { get; private set; }

    public Customer Customer { get; private set; } = null!;

    public Guid? OrderId { get; private set; }

    public Order? Order { get; private set; }

    public LoyaltyTransactionType Type { get; private set; }

    /// <summary>Signed amount: positive for earn/refund, negative for redeem/expire.</summary>
    public decimal Points { get; private set; }

    public decimal BalanceAfter { get; private set; }

    public string Description { get; private set; } = string.Empty;

    public DateTimeOffset? ExpiresAt { get; private set; }

    public bool IsExpired(DateTimeOffset now) => ExpiresAt.HasValue && ExpiresAt.Value <= now;
}

public enum LoyaltyTransactionType
{
    Earn = 0,
    Redeem = 1,
    Expire = 2,
    Refund = 3
}
