using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

public enum IncidentCategory
{
    PowerOutage = 0,
    WaterOrGasOutage = 1,
    InternetOrPos = 2,
    EquipmentFailure = 3,
    SafetyOrHealth = 4,
    CustomerOrService = 5,
    SupplyOrInventory = 6,
    Security = 7,
    Other = 8,
}

public enum IncidentSeverity
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3,
}

public enum IncidentStatus
{
    Open = 0,
    Investigating = 1,
    Resolved = 2,
    Closed = 3,
}

/// <summary>
/// Operational incident log entry for the cafe (e.g. power outage, equipment failure,
/// internet/POS disruption, safety event) with exact occurrence/resolution timestamps,
/// calculated downtime, optional financial impact, and documentary photo.
/// </summary>
public class CafeIncident : BaseAuditableEntity
{
    private CafeIncident()
    {
    }

    public CafeIncident(
        string incidentCode,
        string title,
        IncidentCategory category,
        IncidentSeverity severity,
        DateTimeOffset occurredAt,
        DateTimeOffset? resolvedAt = null,
        IncidentStatus status = IncidentStatus.Open,
        string? location = null,
        string? description = null,
        string? impactSummary = null,
        decimal? estimatedLossAmount = null,
        string? resolutionNotes = null,
        string? reportedByName = null,
        string? resolvedByName = null,
        string? photoUrl = null)
    {
        if (string.IsNullOrWhiteSpace(incidentCode))
        {
            throw new DomainException("کد حادثه الزامی است.");
        }

        IncidentCode = Truncate(incidentCode.Trim(), 32);

        Update(
            title,
            category,
            severity,
            status,
            occurredAt,
            resolvedAt,
            location,
            description,
            impactSummary,
            estimatedLossAmount,
            resolutionNotes,
            reportedByName,
            resolvedByName,
            photoUrl);
    }

    public string IncidentCode { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public IncidentCategory Category { get; private set; } = IncidentCategory.Other;

    public IncidentSeverity Severity { get; private set; } = IncidentSeverity.Medium;

    public IncidentStatus Status { get; private set; } = IncidentStatus.Open;

    public DateTimeOffset OccurredAt { get; private set; }

    public DateTimeOffset? ResolvedAt { get; private set; }

    public string? Location { get; private set; }

    public string? Description { get; private set; }

    public string? ImpactSummary { get; private set; }

    public decimal? EstimatedLossAmount { get; private set; }

    public string? ResolutionNotes { get; private set; }

    public string? ReportedByName { get; private set; }

    public string? ResolvedByName { get; private set; }

    public string? PhotoUrl { get; private set; }

    public int? DurationMinutes =>
        ResolvedAt.HasValue && ResolvedAt.Value >= OccurredAt
            ? (int)Math.Round((ResolvedAt.Value - OccurredAt).TotalMinutes)
            : null;

    public void Update(
        string title,
        IncidentCategory category,
        IncidentSeverity severity,
        IncidentStatus status,
        DateTimeOffset occurredAt,
        DateTimeOffset? resolvedAt,
        string? location,
        string? description,
        string? impactSummary,
        decimal? estimatedLossAmount,
        string? resolutionNotes,
        string? reportedByName,
        string? resolvedByName,
        string? photoUrl)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new DomainException("عنوان حادثه الزامی است.");
        }

        if (resolvedAt.HasValue && resolvedAt.Value < occurredAt)
        {
            throw new DomainException("زمان رفع حادثه نمی‌تواند قبل از زمان وقوع باشد.");
        }

        if (estimatedLossAmount.HasValue && estimatedLossAmount.Value < 0)
        {
            throw new DomainException("مبلغ برآورد خسارت نمی‌تواند منفی باشد.");
        }

        Title = Truncate(title.Trim(), 200);
        Category = category;
        Severity = severity;
        Status = status;
        OccurredAt = occurredAt;
        ResolvedAt = resolvedAt;
        Location = NormalizeOptional(location, 150);
        Description = NormalizeOptional(description, 2000);
        ImpactSummary = NormalizeOptional(impactSummary, 1000);
        EstimatedLossAmount = estimatedLossAmount;
        ResolutionNotes = NormalizeOptional(resolutionNotes, 2000);
        ReportedByName = NormalizeOptional(reportedByName, 150);
        ResolvedByName = NormalizeOptional(resolvedByName, 150);
        PhotoUrl = NormalizeOptional(photoUrl, 500);
    }

    public void MarkResolved(DateTimeOffset resolvedAt, string? resolutionNotes, string? resolvedByName)
    {
        if (resolvedAt < OccurredAt)
        {
            throw new DomainException("زمان رفع حادثه نمی‌تواند قبل از زمان وقوع باشد.");
        }

        Status = IncidentStatus.Resolved;
        ResolvedAt = resolvedAt;
        if (!string.IsNullOrWhiteSpace(resolutionNotes))
        {
            ResolutionNotes = NormalizeOptional(resolutionNotes, 2000);
        }
        if (!string.IsNullOrWhiteSpace(resolvedByName))
        {
            ResolvedByName = NormalizeOptional(resolvedByName, 150);
        }
    }

    private static string Truncate(string value, int maxLen)
        => value.Length > maxLen ? value[..maxLen] : value;

    private static string? NormalizeOptional(string? value, int maxLen)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        return trimmed.Length > maxLen ? trimmed[..maxLen] : trimmed;
    }
}
