using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

/// <summary>
/// Lightweight server-side mirror of a storefront cart. The browser owns the
/// real cart (localStorage); this row lets the reminder sweep see carts that
/// were left behind and issue a single-use comeback code per cart. It is keyed
/// by the anonymous cart token the storefront keeps in localStorage.
/// </summary>
public class AbandonedCart : BaseAuditableEntity
{
    private AbandonedCart()
    {
    }

    public AbandonedCart(
        string cartToken,
        string? customerName,
        string? phoneNumber,
        string? email,
        Guid? customerId,
        string itemsJson,
        decimal itemsTotal,
        int itemCount,
        DateTimeOffset lastActivityAt)
    {
        if (string.IsNullOrWhiteSpace(cartToken))
        {
            throw new DomainException("Cart token is required.");
        }

        CartToken = cartToken.Trim();
        CustomerName = Normalize(customerName);
        PhoneNumber = NormalizePhone(phoneNumber);
        Email = Normalize(email)?.ToLowerInvariant();
        CustomerId = customerId;
        ItemsJson = string.IsNullOrWhiteSpace(itemsJson) ? "[]" : itemsJson;
        ItemsTotal = itemsTotal < 0 ? 0 : itemsTotal;
        ItemCount = itemCount < 0 ? 0 : itemCount;
        LastActivityAt = lastActivityAt;
        Status = AbandonedCartStatus.Active;
    }

    /// <summary>Anonymous token kept by the storefront in localStorage.</summary>
    public string CartToken { get; private set; } = string.Empty;

    public string? CustomerName { get; private set; }

    public string? PhoneNumber { get; private set; }

    public string? Email { get; private set; }

    public Guid? CustomerId { get; private set; }

    /// <summary>Snapshot of the cart lines (menu item, quantity, unit price).</summary>
    public string ItemsJson { get; private set; } = "[]";

    public decimal ItemsTotal { get; private set; }

    public int ItemCount { get; private set; }

    public AbandonedCartStatus Status { get; private set; } = AbandonedCartStatus.Active;

    public DateTimeOffset LastActivityAt { get; private set; }

    public DateTimeOffset? ReminderSentAt { get; private set; }

    /// <summary>The single-use comeback code delivered with the reminder.</summary>
    public string? ReminderCode { get; private set; }

    /// <summary>Channels that actually delivered the reminder (e.g. "sms,email").</summary>
    public string? ReminderChannel { get; private set; }

    public DateTimeOffset? ConvertedAt { get; private set; }

    /// <summary>
    /// Refreshes the snapshot and activity timestamp. A converted/expired cart
    /// that is reused (same token, new shopping session) starts fresh and may
    /// receive a new reminder; an already-reminded cart keeps its code so the
    /// customer never gets duplicate messages for the same cart.
    /// </summary>
    public void Touch(
        string? customerName,
        string? phoneNumber,
        string? email,
        Guid? customerId,
        string itemsJson,
        decimal itemsTotal,
        int itemCount,
        DateTimeOffset lastActivityAt)
    {
        CustomerName = Normalize(customerName) ?? CustomerName;
        PhoneNumber = NormalizePhone(phoneNumber) ?? PhoneNumber;
        Email = Normalize(email)?.ToLowerInvariant() ?? Email;
        CustomerId = customerId ?? CustomerId;
        ItemsJson = string.IsNullOrWhiteSpace(itemsJson) ? "[]" : itemsJson;
        ItemsTotal = itemsTotal < 0 ? 0 : itemsTotal;
        ItemCount = itemCount < 0 ? 0 : itemCount;
        LastActivityAt = lastActivityAt;

        if (Status is AbandonedCartStatus.Converted or AbandonedCartStatus.Expired)
        {
            Status = AbandonedCartStatus.Active;
            ReminderSentAt = null;
            ReminderCode = null;
            ReminderChannel = null;
            ConvertedAt = null;
        }
    }

    public void MarkReminded(string code, string channel, DateTimeOffset sentAt)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new DomainException("Reminder code is required.");
        }

        ReminderCode = code.Trim().ToUpperInvariant();
        ReminderChannel = Normalize(channel);
        ReminderSentAt = sentAt;
        Status = AbandonedCartStatus.Reminded;
    }

    public void MarkConverted(DateTimeOffset convertedAt)
    {
        Status = AbandonedCartStatus.Converted;
        ConvertedAt = convertedAt;
    }

    public void MarkExpired() => Status = AbandonedCartStatus.Expired;

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizePhone(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Replace(" ", string.Empty).Trim();
}
