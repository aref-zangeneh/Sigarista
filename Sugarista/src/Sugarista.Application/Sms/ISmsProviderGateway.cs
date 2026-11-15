using Sugarista.Domain.Entities;

namespace Sugarista.Application.Sms;

/// <summary>
/// Standard result the system maps every provider's raw response into.
/// StatusAccepted/Sent are NOT delivery — only an explicit Delivered is.
/// </summary>
public record SmsSendResult(
    bool Success,
    SmsOutcomeStatus Status,
    string? ProviderMessageId,
    string? ProviderResponse,
    decimal? Cost,
    string? Error)
{
    public static SmsSendResult Ok(string? providerMessageId, string? providerResponse, decimal? cost) =>
        new(true, SmsOutcomeStatus.Sent, providerMessageId, providerResponse, cost, null);

    /// <summary>The provider accepted the message for later delivery.</summary>
    public static SmsSendResult Accepted(string? providerMessageId, string? providerResponse, decimal? cost) =>
        new(true, SmsOutcomeStatus.Accepted, providerMessageId, providerResponse, cost, null);

    public static SmsSendResult Failed(string error) =>
        new(false, SmsOutcomeStatus.Failed, null, null, null, error);

    /// <summary>The provider explicitly refused the message (bad number, blocked, …).</summary>
    public static SmsSendResult Rejected(string error) =>
        new(false, SmsOutcomeStatus.Rejected, null, null, null, error);
}

/// <summary>Result of one bulk call — providers may report per-number results.</summary>
public record SmsBulkResult(
    bool Success,
    string? BatchProviderMessageId,
    IReadOnlyList<SmsBulkItemResult> Items,
    decimal? Cost,
    string? Error)
{
    public static SmsBulkResult Ok(IReadOnlyList<SmsBulkItemResult> items, string? batchId, decimal? cost) =>
        new(true, batchId, items, cost, null);

    public static SmsBulkResult Failed(string error) =>
        new(false, null, [], null, error);
}

public record SmsBulkItemResult(string Mobile, bool Success, string? ProviderMessageId, string? Error);

public record SmsCreditResult(bool Success, decimal? Credit, string? Error);

public record SmsConnectionTestResult(bool Success, string Message, decimal? Credit);

/// <summary>Status a provider reports for a previously sent message.</summary>
public record SmsMessageStatusResult(
    bool Success,
    SmsOutcomeStatus Status,
    decimal? Cost,
    string? Error);

/// <summary>Standardized outcome subset used across adapters.</summary>
public enum SmsOutcomeStatus
{
    Sent = 0,
    Accepted = 1,
    Failed = 2,
    Rejected = 3,
    Delivered = 4,
    Unknown = 5
}

/// <summary>
/// Shared error markers. The AMBIGUOUS-TIMEOUT marker is load-bearing policy:
/// when a request to the provider times out we cannot know whether the SMS
/// was dispatched, so the message is stored as Unknown and NEITHER retried
/// NOR failed over — preventing accidental duplicate sends.
/// </summary>
public static class SmsSendErrors
{
    public const string AmbiguousTimeout =
        "مهلت پاسخ Provider به پایان رسید؛ نتیجه ارسال نامشخص است و برای جلوگیری از ارسال تکراری، تلاش مجدد انجام نمی‌شود.";

    /// <summary>True when the error is the ambiguous-timeout marker.</summary>
    public static bool IsAmbiguousTimeout(string? error) =>
        string.Equals(error, AmbiguousTimeout, StringComparison.Ordinal);
}

/// <summary>
/// The Multi-Provider contract. Every provider adapter implements ONLY what
/// its real API supports — capabilities are declared explicitly and the UI/
/// logic must check them instead of assuming.
/// </summary>
public interface ISmsProviderGateway
{
    /// <summary>Machine-readable adapter id (Logging/Kavenegar/SmsIr/…).</summary>
    SmsProviderType ProviderType { get; }

    bool SupportsSingleSend { get; }

    /// <summary>Native many-numbers-in-one-call API.</summary>
    bool SupportsBulkSend { get; }

    bool SupportsPatternSend { get; }

    bool SupportsCreditCheck { get; }

    bool SupportsMessageStatus { get; }

    bool SupportsDeliveryReport { get; }

    /// <summary>
    /// Provider-side rate guidance: max recipients per single bulk call and
    /// the delay the adapter self-imposes between bulk calls.
    /// </summary>
    int BulkBatchSize { get; }

    Task<SmsSendResult> SendAsync(
        string mobile,
        string message,
        string? senderLine,
        CancellationToken cancellationToken = default);

    Task<SmsBulkResult> SendBulkAsync(
        IReadOnlyList<string> mobiles,
        string message,
        string? senderLine,
        CancellationToken cancellationToken = default);

    /// <summary>Pattern/template send when the provider requires its own template id.</summary>
    Task<SmsSendResult> SendPatternAsync(
        string mobile,
        string providerTemplateId,
        IReadOnlyDictionary<string, string> values,
        string? senderLine,
        CancellationToken cancellationToken = default);

    Task<SmsCreditResult> GetCreditAsync(CancellationToken cancellationToken = default);

    Task<SmsMessageStatusResult> GetMessageStatusAsync(
        string providerMessageId,
        CancellationToken cancellationToken = default);

    Task<SmsConnectionTestResult> TestConnectionAsync(CancellationToken cancellationToken = default);
}
