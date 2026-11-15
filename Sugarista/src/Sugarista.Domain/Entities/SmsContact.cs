using Sugarista.Domain.Common;

namespace Sugarista.Domain.Entities;

/// <summary>
/// A contact in the SMS address book. Mobile is stored NORMALIZED
/// (98912…) with a unique index, so deduplication happens at the data layer.
/// </summary>
public class SmsContact : BaseAuditableEntity
{
    public SmsContact(
        string? firstName,
        string? lastName,
        string mobile,
        string? email,
        string? notes,
        bool isActive,
        bool marketingConsent)
    {
        Update(firstName, lastName, mobile, email, notes, isActive, marketingConsent);
    }

    private SmsContact()
    {
    }

    public string? FirstName { get; private set; }

    public string? LastName { get; private set; }

    public string Mobile { get; private set; } = string.Empty;

    public string? Email { get; private set; }

    public string? Notes { get; private set; }

    /// <summary>Inactive contacts are excluded from every send.</summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// Explicit consent for MARKETING messages. Service messages (order
    /// updates) do not require it.
    /// </summary>
    public bool MarketingConsent { get; private set; }

    public void Update(
        string? firstName,
        string? lastName,
        string mobile,
        string? email,
        string? notes,
        bool isActive,
        bool marketingConsent)
    {
        FirstName = string.IsNullOrWhiteSpace(firstName) ? null : firstName.Trim();
        LastName = string.IsNullOrWhiteSpace(lastName) ? null : lastName.Trim();
        Mobile = mobile;
        Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        IsActive = isActive;
        MarketingConsent = marketingConsent;
    }
}
