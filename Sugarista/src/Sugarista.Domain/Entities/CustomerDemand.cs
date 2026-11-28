using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

public enum CustomerDemandCategory
{
    HotDrink = 0,
    ColdDrink = 1,
    CakeAndDessert = 2,
    BakeryAndFood = 3,
    MilkOrAddon = 4,
    ServiceOrFacility = 5,
    Other = 6,
}

public enum CustomerDemandStatus
{
    Open = 0,
    Planned = 1,
    AddedToMenu = 2,
    Dismissed = 3,
}

/// <summary>
/// Tracks unmet customer requests (items, drinks, milks, desserts, or services
/// customers ask for that the cafe does not currently carry) so management can
/// analyze demand over any date range and make data-driven menu decisions.
/// </summary>
public class CustomerDemand : BaseAuditableEntity
{
    private CustomerDemand()
    {
    }

    public CustomerDemand(
        string requestedItem,
        CustomerDemandCategory category = CustomerDemandCategory.Other,
        int requestedCount = 1,
        DateTimeOffset? requestedAt = null,
        string channel = "POS",
        CustomerDemandStatus status = CustomerDemandStatus.Open,
        string? customerName = null,
        string? customerPhone = null,
        string? notes = null,
        string? recordedByName = null)
    {
        Update(
            requestedItem,
            category,
            requestedCount,
            requestedAt ?? DateTimeOffset.UtcNow,
            channel,
            status,
            customerName,
            customerPhone,
            notes,
            recordedByName);
    }

    public string RequestedItem { get; private set; } = string.Empty;

    /// <summary>Normalized Persian/Latin grouping key so slight spacing/Arabic-letter variations aggregate cleanly.</summary>
    public string NormalizedKey { get; private set; } = string.Empty;

    public CustomerDemandCategory Category { get; private set; } = CustomerDemandCategory.Other;

    public CustomerDemandStatus Status { get; private set; } = CustomerDemandStatus.Open;

    public int RequestedCount { get; private set; } = 1;

    public DateTimeOffset RequestedAt { get; private set; }

    public string Channel { get; private set; } = "POS";

    public string? CustomerName { get; private set; }

    public string? CustomerPhone { get; private set; }

    public string? Notes { get; private set; }

    public string? RecordedByName { get; private set; }

    public void Update(
        string requestedItem,
        CustomerDemandCategory category,
        int requestedCount,
        DateTimeOffset requestedAt,
        string channel,
        CustomerDemandStatus status,
        string? customerName,
        string? customerPhone,
        string? notes,
        string? recordedByName)
    {
        if (string.IsNullOrWhiteSpace(requestedItem))
        {
            throw new DomainException("نام محصول یا نیاز درخواستی مشتری الزامی است.");
        }

        if (requestedCount <= 0)
        {
            throw new DomainException("تعداد درخواست باید حداقل ۱ باشد.");
        }

        var trimmed = Truncate(requestedItem.Trim(), 160);
        RequestedItem = trimmed;
        NormalizedKey = NormalizeDemandKey(trimmed);
        Category = category;
        RequestedCount = requestedCount;
        RequestedAt = requestedAt;
        Channel = string.IsNullOrWhiteSpace(channel) ? "POS" : Truncate(channel.Trim(), 40);
        Status = status;
        CustomerName = NormalizeOptional(customerName, 120);
        CustomerPhone = NormalizeOptional(customerPhone, 30);
        Notes = NormalizeOptional(notes, 500);
        RecordedByName = NormalizeOptional(recordedByName, 150);
    }

    public void SetStatus(CustomerDemandStatus status)
    {
        Status = status;
    }

    public static string NormalizeDemandKey(string raw)
    {
        var normalized = (raw ?? string.Empty)
            .Trim()
            .ToLowerInvariant()
            .Replace('ي', 'ی')
            .Replace('ك', 'ک')
            .Replace('\u200c', ' ');

        var parts = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var joined = string.Join(' ', parts);
        return joined.Length > 160 ? joined[..160] : joined;
    }

    private static string Truncate(string value, int maxLen)
        => value.Length > maxLen ? value[..maxLen] : value;

    private static string? NormalizeOptional(string? value, int maxLen)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        return trimmed.Length > maxLen ? trimmed[..maxLen] : trimmed;
    }
}
