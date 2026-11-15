using Sugarista.Domain.Common;

namespace Sugarista.Domain.Entities;

/// <summary>
/// A configured SMS service provider (Kavenegar, SMS.ir, …). Credentials are
/// stored ENCRYPTED (Encrypted* fields hold DataProtection ciphertext) and are
/// never returned to the admin panel — responses only carry masked previews
/// and "has credential" flags.
/// </summary>
public class SmsProvider : BaseAuditableEntity
{
    public SmsProvider(
        string name,
        SmsProviderType providerType,
        string? baseUrl,
        string? encryptedApiKey,
        string? encryptedUsername,
        string? encryptedPassword,
        int timeoutSeconds,
        string? extraSettingsJson,
        bool isEnabled,
        bool isDefault)
    {
        Id = Guid.NewGuid();
        Update(name, providerType, baseUrl, encryptedApiKey, encryptedUsername, encryptedPassword, timeoutSeconds, extraSettingsJson, isEnabled, isDefault);
    }

    private SmsProvider()
    {
    }

    public string Name { get; private set; } = string.Empty;

    public SmsProviderType ProviderType { get; private set; }

    /// <summary>Optional API base URL override; null = adapter default.</summary>
    public string? BaseUrl { get; private set; }

    /// <summary>DataProtection ciphertext — never plaintext, never returned.</summary>
    public string? EncryptedApiKey { get; private set; }

    public string? EncryptedUsername { get; private set; }

    public string? EncryptedPassword { get; private set; }

    public bool IsEnabled { get; private set; }

    /// <summary>Exactly one enabled provider should be the default fallback.</summary>
    public bool IsDefault { get; private set; }

    public int TimeoutSeconds { get; private set; }

    /// <summary>Provider-specific knobs the adapter understands (JSON).</summary>
    public string? ExtraSettingsJson { get; private set; }

    /// <summary>
    /// Shared secret for the DELIVERY-REPORT webhook URL the admin configures
    /// in the provider panel (api/sms/webhook/{providerId}/{secret}). Not a
    /// credential — it must be visible so the panel can show the full URL.
    /// </summary>
    public string? DeliveryWebhookSecret { get; private set; }

    /// <summary>Last time an API call to this provider succeeded.</summary>
    public DateTimeOffset? LastSuccessfulConnectionAt { get; private set; }

    public string? LastConnectionError { get; private set; }

    public DateTimeOffset? LastConnectionErrorAt { get; private set; }

    public List<SmsSenderLine> SenderLines { get; set; } = [];

    public void Update(
        string name,
        SmsProviderType providerType,
        string? baseUrl,
        string? encryptedApiKey,
        string? encryptedUsername,
        string? encryptedPassword,
        int timeoutSeconds,
        string? extraSettingsJson,
        bool isEnabled,
        bool isDefault)
    {
        Name = name.Trim();
        ProviderType = providerType;
        BaseUrl = string.IsNullOrWhiteSpace(baseUrl) ? null : baseUrl.Trim();
        // null = keep the previously stored credential (admin left the field empty).
        EncryptedApiKey = encryptedApiKey;
        EncryptedUsername = encryptedUsername;
        EncryptedPassword = encryptedPassword;
        IsEnabled = isEnabled;
        IsDefault = isDefault;
        TimeoutSeconds = timeoutSeconds is <= 0 or > 120 ? 30 : timeoutSeconds;
        ExtraSettingsJson = string.IsNullOrWhiteSpace(extraSettingsJson) ? null : extraSettingsJson;
    }

    /// <summary>Sets (or regenerates) the delivery-report webhook secret.</summary>
    public void SetDeliveryWebhookSecret(string secret) =>
        DeliveryWebhookSecret = string.IsNullOrWhiteSpace(secret)
            ? null
            : secret.Trim();

    public void MarkConnectionSucceeded(DateTimeOffset atUtc)
    {
        LastSuccessfulConnectionAt = atUtc;
        LastConnectionError = null;
        LastConnectionErrorAt = null;
    }

    public void MarkConnectionFailed(string error, DateTimeOffset atUtc)
    {
        LastConnectionError = error.Length > 500 ? error[..500] : error;
        LastConnectionErrorAt = atUtc;
    }
}
