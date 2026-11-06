using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

/// <summary>
/// A pre-defined wallet charge package (e.g. charge 500.000 toman, get 50.000
/// toman bonus). The admin curates these; the POS and the online wallet panel
/// offer them as quick-charge options so bonuses stay consistent everywhere.
/// </summary>
public class WalletTopUpPackage : BaseAuditableEntity
{
    public WalletTopUpPackage(decimal amount, decimal bonusAmount, int displayOrder = 0, bool isEnabled = true)
    {
        Update(amount, bonusAmount, displayOrder, isEnabled);
    }

    private WalletTopUpPackage()
    {
    }

    /// <summary>Money the customer hands over (toman). Must be positive.</summary>
    public decimal Amount { get; private set; }

    /// <summary>Gift credit granted on top of the charge (toman). May be zero.</summary>
    public decimal BonusAmount { get; private set; }

    public int DisplayOrder { get; private set; }

    public bool IsEnabled { get; private set; }

    public void Update(decimal amount, decimal bonusAmount, int displayOrder, bool isEnabled)
    {
        if (amount <= 0)
        {
            throw new DomainException("Top-up package amount must be positive.");
        }

        if (bonusAmount < 0)
        {
            throw new DomainException("Top-up package bonus cannot be negative.");
        }

        Amount = amount;
        BonusAmount = bonusAmount;
        DisplayOrder = displayOrder;
        IsEnabled = isEnabled;
    }
}
