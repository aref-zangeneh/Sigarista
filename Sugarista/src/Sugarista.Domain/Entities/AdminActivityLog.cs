using Sugarista.Domain.Common;

namespace Sugarista.Domain.Entities;

/// <summary>
/// One human-readable record of what an admin user did inside the panel.
/// The log is append-only: it is never edited, only purged by retention rules.
/// </summary>
public class AdminActivityLog : BaseEntity
{
    private AdminActivityLog()
    {
    }

    public AdminActivityLog(
        Guid? actorId,
        string actorUsername,
        string? actorDisplayName,
        string action,
        string sectionId,
        string sectionTitle,
        string? entityLabel,
        string description,
        IReadOnlyCollection<string> changedFields,
        string httpMethod,
        string path,
        int statusCode,
        bool succeeded,
        string? ipAddress,
        string? userAgent,
        DateTimeOffset occurredAt)
    {
        ActorId = actorId;
        ActorUsername = actorUsername.Trim();
        ActorDisplayName = Normalize(actorDisplayName);
        Action = action;
        SectionId = sectionId;
        SectionTitle = sectionTitle;
        EntityLabel = Normalize(entityLabel);
        Description = description;
        ChangedFields = changedFields.Count == 0 ? null : string.Join('|', changedFields);
        HttpMethod = httpMethod;
        Path = path;
        StatusCode = statusCode;
        Succeeded = succeeded;
        IpAddress = Normalize(ipAddress);
        UserAgent = Normalize(userAgent);
        OccurredAt = occurredAt;
    }

    public Guid? ActorId { get; private set; }

    public string ActorUsername { get; private set; } = string.Empty;

    public string? ActorDisplayName { get; private set; }

    /// <summary>login | logout | create | update | delete | status | upload | export | other</summary>
    public string Action { get; private set; } = string.Empty;

    public string SectionId { get; private set; } = string.Empty;

    public string SectionTitle { get; private set; } = string.Empty;

    public string? EntityLabel { get; private set; }

    /// <summary>Ready-to-read Persian sentence displayed in the timeline.</summary>
    public string Description { get; private set; } = string.Empty;

    /// <summary>Persian names of the fields the user touched, joined with '|'.</summary>
    public string? ChangedFields { get; private set; }

    public string HttpMethod { get; private set; } = string.Empty;

    public string Path { get; private set; } = string.Empty;

    public int StatusCode { get; private set; }

    public bool Succeeded { get; private set; }

    public string? IpAddress { get; private set; }

    public string? UserAgent { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public IReadOnlyList<string> ChangedFieldList =>
        string.IsNullOrWhiteSpace(ChangedFields)
            ? []
            : ChangedFields.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
