using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

/// <summary>
/// Unified customer profile across all channels (POS, online orders):
/// matched by phone number so the customer club stays a single accurate identity.
/// </summary>
public class Customer : BaseAuditableEntity
{
    public Customer(string persianName, string phoneNumber, string firstChannel, string? email = null, string? notes = null, string? referralCode = null)
    {
        if (string.IsNullOrWhiteSpace(persianName))
        {
            throw new DomainException("Customer name is required.");
        }

        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            throw new DomainException("Customer phone number is required.");
        }

        if (string.IsNullOrWhiteSpace(firstChannel))
        {
            throw new DomainException("Customer source channel is required.");
        }

        PersianName = persianName.Trim();
        PhoneNumber = phoneNumber.Trim();
        Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        FirstChannel = firstChannel.Trim().ToLowerInvariant();
        LastChannel = FirstChannel;
        IsActive = true;
        ReferralCode = string.IsNullOrWhiteSpace(referralCode) ? GenerateReferralCode() : referralCode.Trim().ToUpperInvariant();
    }

    public string PersianName { get; private set; } = string.Empty;

    public string PhoneNumber { get; private set; } = string.Empty;

    public string? Email { get; private set; }

    public string? Notes { get; private set; }

    /// <summary>First channel that introduced this customer (pos / online).</summary>
    public string FirstChannel { get; private set; } = string.Empty;

    /// <summary>Most recent channel used by this customer.</summary>
    public string LastChannel { get; private set; } = string.Empty;

    public bool IsActive { get; private set; } = true;

    /// <summary>When a win-back discount SMS was last sent to this customer (drives the win-back dispatch).</summary>
    public DateTimeOffset? WinBackSentAt { get; private set; }

    /// <summary>Personal referral code of this customer (shared as an invite link/code).</summary>
    public string? ReferralCode { get; private set; }

    /// <summary>True when this customer (as referrer) earned a referral reward — set after the referred customer completed a successful first order.</summary>
    public bool ReferralRewardEligible { get; private set; }

    public ICollection<Order> Orders { get; private set; } = [];

    public decimal LoyaltyPoints { get; private set; }

    public ICollection<LoyaltyTransaction> LoyaltyTransactions { get; private set; } = [];

    /// <summary>Current membership level — reassigned automatically by the tier evaluation.</summary>
    public Guid? MembershipTierId { get; private set; }

    public MembershipTier? MembershipTier { get; private set; }

    /// <summary>When the membership level was last (re)assigned.</summary>
    public DateTimeOffset? MembershipTierAssignedAt { get; private set; }

    public void SetMembershipTier(Guid? tierId)
    {
        MembershipTierId = tierId;
        MembershipTierAssignedAt = tierId is null ? null : DateTimeOffset.UtcNow;
    }

    public void AddLoyaltyPoints(decimal points)
    {
        if (points < 0)
        {
            throw new DomainException("Loyalty points cannot be negative.");
        }

        LoyaltyPoints += points;
    }

    public void DeductLoyaltyPoints(decimal points)
    {
        if (points < 0)
        {
            throw new DomainException("Loyalty points cannot be negative.");
        }

        if (points > LoyaltyPoints)
        {
            throw new DomainException("Insufficient loyalty points.");
        }

        LoyaltyPoints -= points;
    }

    public void MarkWinBackSent(DateTimeOffset sentAt) => WinBackSentAt = sentAt;

    public void MarkReferralRewardEligible() => ReferralRewardEligible = true;

    public static string GenerateReferralCode()
        => $"REF-{Guid.NewGuid().ToString("N").Substring(0, 6).ToUpperInvariant()}";

    public void Update(string persianName, string? email, string? notes, bool isActive)
    {
        if (string.IsNullOrWhiteSpace(persianName))
        {
            throw new DomainException("Customer name is required.");
        }

        PersianName = persianName.Trim();
        Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        IsActive = isActive;
    }

    public void MarkSeen(string channel)
    {
        if (!string.IsNullOrWhiteSpace(channel))
        {
            LastChannel = channel.Trim().ToLowerInvariant();
        }
    }
}
