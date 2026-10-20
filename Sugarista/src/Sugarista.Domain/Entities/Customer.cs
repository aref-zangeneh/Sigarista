using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

/// <summary>
/// Unified customer profile across all channels (POS, online orders):
/// matched by phone number so the customer club stays a single accurate identity.
/// </summary>
public class Customer : BaseAuditableEntity
{
    public Customer(string persianName, string phoneNumber, string firstChannel, string? email = null, string? notes = null)
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

    public ICollection<Order> Orders { get; private set; } = [];

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
