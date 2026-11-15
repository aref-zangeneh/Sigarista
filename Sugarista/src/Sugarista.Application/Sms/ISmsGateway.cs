using Sugarista.Domain.Entities;

namespace Sugarista.Application.Sms;

/// <summary>
/// Orchestrates every send: normalization/validation, opt-out filtering,
/// campaign creation, batch assignment and provider calls. Backs both the
/// admin endpoints and the internal system notifications (ISmsService).
/// </summary>
public interface ISmsGateway
{
    /// <summary>
    /// Sends one message immediately (synchronous request path). Used by
    /// single-send UI and small system notifications.
    /// </summary>
    Task<SmsSendOutcome> SendSingleAsync(
        SmsSendRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a recipient list for a bulk send WITHOUT sending: returns
    /// valid/deduped/opted-out/invalid partitions + cost estimate. The panel
    /// shows this before asking for confirmation.
    /// </summary>
    Task<SmsBulkPreview> PreviewBulkAsync(
        SmsBulkPreviewRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Creates the campaign + per-recipient message rows (Pending); the background runner sends them.</summary>
    Task<SmsCampaign> CreateCampaignAsync(
        SmsBulkPreviewRequest request,
        string title,
        string? createdBy,
        CancellationToken cancellationToken = default);

    /// <summary>Sends one system-notification message using the default provider (best-effort, never throws).</summary>
    Task<bool> SendSystemAsync(
        string? mobile,
        string message,
        string action,
        CancellationToken cancellationToken = default);
}

public sealed record SmsSendRequest(
    string? Mobile,
    Guid? ProviderId,
    Guid? TemplateId,
    string? Body,
    SmsMessageKind Kind,
    string? SenderLine,
    string? RequestedBy);

public sealed record SmsSendOutcome(
    bool Success,
    Guid? MessageId,
    SmsSendStatus Status,
    string? ProviderMessageId,
    string? Error,
    string NormalizedMobile);

public sealed record SmsBulkPreviewRequest(
    IReadOnlyList<string> Recipients,
    IReadOnlyList<Guid> GroupIds,
    Guid? ProviderId,
    Guid? TemplateId,
    string? Body,
    SmsMessageKind Kind,
    string? SenderLine = null);

public sealed record SmsBulkPreview(
    IReadOnlyList<string> ValidRecipients,
    IReadOnlyList<string> InvalidRecipients,
    IReadOnlyList<string> DeduplicatedRecipients,
    IReadOnlyList<string> OptedOutRecipients,
    IReadOnlyList<string> InactiveRecipients,
    int TotalMessages,
    int TotalSegments,
    string RenderedBody,
    int Segments,
    int Characters,
    bool IsUnicode,
    SmsProviderCapabilities ProviderCapabilities);

public sealed record SmsProviderCapabilities(
    string ProviderName,
    bool SupportsBulk,
    int BulkBatchSize,
    bool SupportsCredit,
    decimal? Credit);
