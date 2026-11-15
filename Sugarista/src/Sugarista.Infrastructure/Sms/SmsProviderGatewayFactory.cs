using Microsoft.Extensions.Logging;
using Sugarista.Application.Sms;
using Sugarista.Domain.Entities;

namespace Sugarista.Infrastructure.Sms;

/// <summary>
/// Builds a gateway for ONE provider call with the given (already decrypted)
/// credentials. Multiple providers of the same type coexist fine because the
/// credential is supplied per call, not baked into a singleton.
/// </summary>
public sealed class SmsProviderGatewayFactory(
    IHttpClientFactory httpClientFactory,
    ILoggerFactory loggerFactory) : ISmsProviderGatewayFactory
{
    public ISmsProviderGateway Create(SmsProviderCredentials credentials)
    {
        var httpClient = httpClientFactory.CreateClient("SmsProvider");
        httpClient.Timeout = TimeSpan.FromSeconds(Math.Clamp(credentials.TimeoutSeconds, 3, 120));

        return credentials.ProviderType switch
        {
            SmsProviderType.Kavenegar => new KavenegarGateway(
                httpClient,
                loggerFactory.CreateLogger<KavenegarGateway>(),
                credentials.ApiKey,
                credentials.BaseUrl),
            SmsProviderType.SmsIr => new SmsIrGateway(
                httpClient,
                loggerFactory.CreateLogger<SmsIrGateway>(),
                credentials.ApiKey,
                credentials.BaseUrl),
            SmsProviderType.Melipayamak => new MelipayamakGateway(
                httpClient,
                loggerFactory.CreateLogger<MelipayamakGateway>(),
                credentials.Username,
                credentials.Password,
                credentials.BaseUrl),
            SmsProviderType.Ghasedak => new GhasedakGateway(
                httpClient,
                loggerFactory.CreateLogger<GhasedakGateway>(),
                credentials.ApiKey,
                credentials.BaseUrl),
            SmsProviderType.IpPanel => new IpPanelGateway(
                httpClient,
                loggerFactory.CreateLogger<IpPanelGateway>(),
                credentials.ApiKey,
                credentials.BaseUrl),
            _ => new LoggingGateway(loggerFactory.CreateLogger<LoggingGateway>())
        };
    }
}
