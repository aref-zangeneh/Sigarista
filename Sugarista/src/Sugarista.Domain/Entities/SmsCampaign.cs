using Sugarista.Domain.Common;

namespace Sugarista.Domain.Entities;

/// <summary>
/// A bulk send created from the admin panel. The runner processes it in
/// background batches; every recipient gets its own SmsMessage row so the
/// per-recipient report is independent.
/// </summary>
public class SmsCampaign : BaseAuditableEntity
{
    public SmsCampaign(
        string title,
        string body,
        SmsMessageKind kind,
        Guid? templateId,
        Guid providerId,
        IReadOnlyCollection<string> recipients,
        string? createdBy,
        DateTimeOffset? scheduledAt = null,
        string? senderLine = null)
    {
        Title = title.Trim();
        Body = body;
        Kind = kind;
        TemplateId = templateId;
        ProviderId = providerId;
        TotalRecipients = recipients.Count;
        CreatedBy = createdBy;
        CreatedAt = DateTimeOffset.UtcNow;
        ScheduledAt = scheduledAt;
        SenderLine = string.IsNullOrWhiteSpace(senderLine) ? null : senderLine.Trim();
        Status = SmsCampaignStatus.Pending;
    }

    private SmsCampaign()
    {
    }

    public string Title { get; private set; } = string.Empty;

    public string Body { get; private set; } = string.Empty;

    public SmsMessageKind Kind { get; private set; }

    public Guid? TemplateId { get; private set; }

    public Guid ProviderId { get; private set; }

    public SmsCampaignStatus Status { get; private set; }

    /// <summary>
    /// When set, the runner defers the campaign until this moment (UTC).
    /// Scheduled sends stay Pending and are picked up only when due.
    /// </summary>
    public DateTimeOffset? ScheduledAt { get; private set; }

    /// <summary>Sender line chosen by the admin; null = provider default line.</summary>
    public string? SenderLine { get; private set; }

    public int TotalRecipients { get; private set; }

    public int SentCount { get; private set; }

    public int FailedCount { get; private set; }

    /// <summary>Set by the admin panel to stop a pending/running campaign.</summary>
    public bool CancelRequested { get; private set; }

    public DateTimeOffset? StartedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public void MarkProcessing(DateTimeOffset atUtc)
    {
        Status = SmsCampaignStatus.Processing;
        StartedAt ??= atUtc;
    }

    public void UpdateProgress(int sentCount, int failedCount)
    {
        SentCount = sentCount;
        FailedCount = failedCount;
    }

    public void MarkCompleted(DateTimeOffset atUtc)
    {
        Status = SmsCampaignStatus.Completed;
        CompletedAt = atUtc;
    }

    public void MarkCancelled(DateTimeOffset atUtc)
    {
        Status = SmsCampaignStatus.Cancelled;
        CompletedAt = atUtc;
    }

    public void MarkFailed(DateTimeOffset atUtc)
    {
        Status = SmsCampaignStatus.Failed;
        CompletedAt = atUtc;
    }

    public void RequestCancel() => CancelRequested = true;
}
