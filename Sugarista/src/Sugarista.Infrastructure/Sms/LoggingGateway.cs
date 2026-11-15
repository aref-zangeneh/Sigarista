using Microsoft.Extensions.Logging;
using Sugarista.Application.Sms;

namespace Sugarista.Infrastructure.Sms;

/// <summary>
/// Logging-only provider for development/testing: every send "succeeds" and
/// the message goes to the application log. Nothing leaves the machine.
/// </summary>
public sealed class LoggingGateway(ILogger<LoggingGateway> logger) : ISmsProviderGateway
{
    public Domain.Entities.SmsProviderType ProviderType => Domain.Entities.SmsProviderType.Logging;

    public bool SupportsSingleSend => true;
    public bool SupportsBulkSend => true;
    public bool SupportsPatternSend => true;
    public bool SupportsCreditCheck => true;
    public bool SupportsMessageStatus => false;
    public bool SupportsDeliveryReport => false;
    public int BulkBatchSize => 100;

    public Task<SmsSendResult> SendAsync(
        string mobile,
        string message,
        string? senderLine,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "SMS (Logging provider) → {Mobile} via {Sender}: {Message}",
            mobile, senderLine ?? "(default)", message);

        return Task.FromResult(SmsSendResult.Ok(Guid.NewGuid().ToString(), "Logged", null));
    }

    public Task<SmsBulkResult> SendBulkAsync(
        IReadOnlyList<string> mobiles,
        string message,
        string? senderLine,
        CancellationToken cancellationToken = default)
    {
        foreach (var mobile in mobiles)
        {
            logger.LogInformation("SMS (Logging provider, bulk) → {Mobile}: {Message}", mobile, message);
        }

        return Task.FromResult(SmsBulkResult.Ok(
            mobiles.Select(mobile => new SmsBulkItemResult(mobile, true, Guid.NewGuid().ToString(), null)).ToList(),
            Guid.NewGuid().ToString(),
            null));
    }

    public Task<SmsSendResult> SendPatternAsync(
        string mobile,
        string providerTemplateId,
        IReadOnlyDictionary<string, string> values,
        string? senderLine,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "SMS (Logging provider, pattern {Template}) → {Mobile}: {Values}",
            providerTemplateId, mobile, string.Join(", ", values.Select(pair => $"{pair.Key}={pair.Value}")));

        return Task.FromResult(SmsSendResult.Ok(Guid.NewGuid().ToString(), "Logged", null));
    }

    public Task<SmsCreditResult> GetCreditAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new SmsCreditResult(true, 999999m, null));

    public Task<SmsMessageStatusResult> GetMessageStatusAsync(
        string providerMessageId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new SmsMessageStatusResult(false, SmsOutcomeStatus.Unknown, null,
            "Provider Logging وضعیت پیامک را پیگیری نمی‌کند."));

    public Task<SmsConnectionTestResult> TestConnectionAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new SmsConnectionTestResult(true, "Provider Logging فعال است (پیامک‌ها فقط ثبت می‌شوند).", 999999m));
}
