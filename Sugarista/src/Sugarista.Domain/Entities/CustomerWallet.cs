using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

/// <summary>
/// Per-customer wallet: holds the cash balance the customer charged (in the
/// POS) and cashback the cafe granted. Purchase payments spend from it.
/// The domain keeps the balance honest: money never leaves it implicitly and
/// it can never go below zero.
/// </summary>
public class CustomerWallet : BaseAuditableEntity
{
    private CustomerWallet()
    {
    }

    public CustomerWallet(Guid customerId)
    {
        if (customerId == Guid.Empty)
        {
            throw new DomainException("Wallet customer id is required.");
        }

        CustomerId = customerId;
    }

    public Guid CustomerId { get; private set; }

    public Customer Customer { get; private set; } = null!;

    /// <summary>Current cash balance (toman). Always >= 0.</summary>
    public decimal Balance { get; private set; }

    public void Credit(decimal amount)
    {
        if (amount <= 0)
        {
            throw new DomainException("Wallet credit amount must be positive.");
        }

        Balance += amount;
    }

    public void Debit(decimal amount)
    {
        if (amount <= 0)
        {
            throw new DomainException("Wallet payment amount must be positive.");
        }

        if (amount > Balance)
        {
            throw new DomainException("موجودی کیف پول کافی نیست.");
        }

        Balance -= amount;
    }
}
