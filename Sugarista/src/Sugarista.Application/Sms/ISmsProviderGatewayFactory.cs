using Sugarista.Domain.Entities;

namespace Sugarista.Application.Sms;

/// <summary>
/// Creates the right gateway for a configured provider entity. Credentials
/// are passed DECRYPTED by the resolver — adapters never see ciphertext.
/// </summary>
public interface ISmsProviderGatewayFactory
{
    ISmsProviderGateway Create(SmsProviderCredentials credentials);
}

/// <summary>Decrypted, validated view of a provider configuration.</summary>
public sealed record SmsProviderCredentials(
    Guid Id,
    string Name,
    SmsProviderType ProviderType,
    string? BaseUrl,
    string? ApiKey,
    string? Username,
    string? Password,
    int TimeoutSeconds,
    string? ExtraSettingsJson,
    string? DefaultSenderLine);
