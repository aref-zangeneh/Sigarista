using Sugarista.Domain.Entities;

namespace Sugarista.Application.Sms;

/// <summary>
/// One gateway call the SmsGateway makes on a provider's behalf. Returns
/// nothing — implementors record SmsProviderLog rows and update the
/// provider's connection health themselves.
/// </summary>
public interface ISmsProviderLog
{
    void Record(
        SmsProvider? provider,
        string action,
        bool success,
        string? detail);
}

/// <summary>
/// Resolves the configured provider to use and decrypts its credentials.
/// Also decides failover (allowed when the failure is PROVIDER-SIDE, never
/// for validation problems or ambiguous timeouts).
/// </summary>
public interface ISmsProviderResolver
{
    Task<SmsProvider?> ResolveDefaultAsync(CancellationToken cancellationToken = default);

    Task<SmsProvider?> ResolveByIdAsync(Guid providerId, CancellationToken cancellationToken = default);

    /// <summary>The configured failover target for a provider, when enabled and different.</summary>
    Task<SmsProvider?> ResolveFailoverAsync(Guid primaryProviderId, CancellationToken cancellationToken = default);

    SmsProviderCredentials Decrypt(SmsProvider provider);

    Task RecordLogAsync(
        SmsProvider? provider,
        string action,
        bool success,
        string? detail,
        CancellationToken cancellationToken = default);
}
