using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sugarista.Application.Sms;

/// <summary>
/// Smart (dynamic) group definition: members are computed at read/send time
/// from these predicates instead of a fixed membership table. JSON-serializable
/// and stored on SmsContactGroup.SmartFilterJson.
/// </summary>
public sealed record SmsSmartContactFilter(
    bool? IsActive,
    bool? MarketingConsent,
    int? AddedWithinDays)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static readonly SmsSmartContactFilter Default = new(null, null, null);

    public bool IsEmpty =>
        IsActive is null && MarketingConsent is null && (AddedWithinDays is null or <= 0);

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public static SmsSmartContactFilter? FromJsonOrNull(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<SmsSmartContactFilter>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Persian summary for the admin UI / audit trail.</summary>
    public string Describe()
    {
        var parts = new List<string>();
        if (IsActive is { } active)
        {
            parts.Add(active ? "مخاطب فعال" : "مخاطب غیرفعال");
        }
        if (MarketingConsent is { } consent)
        {
            parts.Add(consent ? "دارای رضایت تبلیغات" : "بدون رضایت تبلیغات");
        }
        if (AddedWithinDays is > 0)
        {
            parts.Add($"اضافه‌شده در {AddedWithinDays} روز اخیر");
        }
        return parts.Count == 0 ? "همهٔ مخاطبین" : string.Join("، ", parts);
    }
}
