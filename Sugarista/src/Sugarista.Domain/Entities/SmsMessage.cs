using Sugarista.Domain.Common;

namespace Sugarista.Domain.Entities;

/// <summary>
/// One SMS addressed to one recipient — the per-recipient record every bulk
/// campaign row also points at (CampaignId). Send result and delivery state
/// are tracked here, independently per recipient.
/// </summary>
public class SmsMessage : BaseAuditableEntity
{
    public SmsMessage(
        Guid? providerId,
        string mobile,
        string body,
        SmsMessageKind kind,
        SmsSendType sendType,
        Guid? templateId,
        Guid? campaignId,
        int batchNumber,
        string? requestedBy)
    {
        ProviderId = providerId;
        Mobile = mobile;
        Body = body;
        Kind = kind;
        SendType = sendType;
        TemplateId = templateId;
        CampaignId = campaignId;
        BatchNumber = batchNumber;
        RequestedBy = requestedBy;
        RequestedAt = DateTimeOffset.UtcNow;
        Status = SmsSendStatus.Pending;
    }

    private SmsMessage()
    {
    }

    public Guid? ProviderId { get; private set; }

    /// <summary>Normalized Iranian mobile, e.g. 989121234567.</summary>
    public string Mobile { get; private set; } = string.Empty;

    public string Body { get; private set; } = string.Empty;

    public SmsMessageKind Kind { get; private set; }

    public SmsSendType SendType { get; private set; }

    public Guid? TemplateId { get; private set; }

    /// <summary>Set when this row belongs to a bulk campaign.</summary>
    public Guid? CampaignId { get; private set; }

    /// <summary>1-based chunk inside the campaign the row was sent with.</summary>
    public int BatchNumber { get; private set; }

    public string? RequestedBy { get; private set; }

    public DateTimeOffset RequestedAt { get; private set; }

    public DateTimeOffset? SentAt { get; private set; }

    public SmsSendStatus Status { get; private set; }

    /// <summary>Provider's own message identifier (for status lookups).</summary>
    public string? ProviderMessageId { get; private set; }

    /// <summary>Short provider response summary for the history UI.</summary>
    public string? ProviderResponse { get; private set; }

    /// <summary>Cost the provider reported, when it reports one.</summary>
    public decimal? Cost { get; private set; }

    public int Segments { get; private set; }

    /// <summary>Which failover attempt produced this message (0 = primary).</summary>
    public int AttemptNumber { get; private set; }

    public string? ErrorMessage { get; private set; }

    public DateTimeOffset? LastStatusAt { get; private set; }

    public void MarkQueued()
    {
        Status = SmsSendStatus.Queued;
        LastStatusAt = DateTimeOffset.UtcNow;
    }

    public void MarkProcessing()
    {
        Status = SmsSendStatus.Processing;
        LastStatusAt = DateTimeOffset.UtcNow;
    }

    public void MarkResult(
        SmsSendStatus status,
        string? providerMessageId,
        string? providerResponse,
        decimal? cost,
        string? error)
    {
        Status = status;
        ProviderMessageId = providerMessageId ?? ProviderMessageId;
        ProviderResponse = providerResponse;
        Cost = cost ?? Cost;
        ErrorMessage = string.IsNullOrWhiteSpace(error) ? null : error;
        if (status is SmsSendStatus.Accepted or SmsSendStatus.Sent)
        {
            SentAt = DateTimeOffset.UtcNow;
        }
        LastStatusAt = DateTimeOffset.UtcNow;
    }

    public void MarkDelivery(SmsSendStatus status)
    {
        Status = status;
        LastStatusAt = DateTimeOffset.UtcNow;
    }

    public void AttachAttempt(Guid? providerId, int attemptNumber)
    {
        ProviderId = providerId;
        AttemptNumber = attemptNumber;
        Status = SmsSendStatus.Processing;
        LastStatusAt = DateTimeOffset.UtcNow;
    }
}
