using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

public enum WalletTransactionType
{
    /// <summary>Money charged into the wallet (POS cash charge).</summary>
    TopUp = 0,

    /// <summary>Percent cashback granted after a paid order.</summary>
    Cashback = 1,

    /// <summary>Money spent from the wallet to pay an order (signed negative).</summary>
    Purchase = 2,

    /// <summary>Money returned to the wallet when a paid order is cancelled (signed positive).</summary>
    Refund = 3,

    /// <summary>Explicit admin correction (signed both ways).</summary>
    Adjustment = 4
}

/// <summary>
/// Immutable audit line for every wallet movement. Every credit or debit the
/// wallet ever receives ends up here with the resulting balance, so the
/// balance can always be replayed from the log alone.
/// </summary>
public class WalletTransaction : BaseAuditableEntity
{
    private WalletTransaction()
    {
    }

    public WalletTransaction(
        Guid walletId,
        WalletTransactionType type,
        decimal amount,
        decimal balanceAfter,
        string description,
        Guid? orderId = null,
        DateTimeOffset? expiresAt = null)
    {
        if (amount == 0)
        {
            throw new DomainException("Wallet transaction amount cannot be zero.");
        }

        if (balanceAfter < 0)
        {
            throw new DomainException("Wallet balance cannot be negative.");
        }

        WalletId = walletId;
        Type = type;
        Amount = amount;
        BalanceAfter = balanceAfter;
        Description = string.IsNullOrWhiteSpace(description) ? type.ToString() : description.Trim();
        OrderId = orderId;
        ExpiresAt = expiresAt;
    }

    public Guid WalletId { get; private set; }

    public CustomerWallet Wallet { get; private set; } = null!;

    public WalletTransactionType Type { get; private set; }

    /// <summary>Signed movement: positive in, negative out. Adjustment type may be either.</summary>
    public decimal Amount { get; private set; }

    public decimal BalanceAfter { get; private set; }

    public string Description { get; private set; } = string.Empty;

    public Guid? OrderId { get; private set; }

    public Order? Order { get; private set; }

    /// <summary>
    /// For cashback movements: the moment this granted credit expires out of
    /// the wallet (null = never expires). Cleared once the expiry is logged.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; private set; }

    /// <summary>
    /// Clears the expiry marker of an already retired credit lot, so the
    /// expiry sweep stays idempotent without editing the transaction log.
    /// </summary>
    public void MarkExpiryLogged()
    {
        ExpiresAt = null;
    }
}
