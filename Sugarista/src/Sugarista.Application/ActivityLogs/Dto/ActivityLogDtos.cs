using Sugarista.Domain.Entities;

namespace Sugarista.Application.ActivityLogs.Dto;

public record AdminActivityLogDto(
    Guid Id,
    Guid? ActorId,
    string ActorUsername,
    string? ActorDisplayName,
    string Action,
    string ActionTitle,
    string SectionId,
    string SectionTitle,
    string? EntityLabel,
    string Description,
    IReadOnlyList<string> ChangedFields,
    string HttpMethod,
    string Path,
    int StatusCode,
    bool Succeeded,
    string? IpAddress,
    string? UserAgent,
    DateTimeOffset OccurredAt);

public record ActivityLogSummaryDto(
    int TotalEvents,
    int TodayEvents,
    int FailedToday,
    int LoginsToday,
    int ActiveUsersInWeek,
    DateTimeOffset? LastEventAt);

public record ActivityLogActorFilterDto(string Username, string? DisplayName, int Count);

public record ActivityLogSectionFilterDto(string SectionId, string SectionTitle, int Count);

public record ActivityLogFiltersDto(
    IReadOnlyList<ActivityLogActorFilterDto> Actors,
    IReadOnlyList<ActivityLogSectionFilterDto> Sections,
    IReadOnlyList<string> Actions);

public static class ActivityLogMapper
{
    public static AdminActivityLogDto ToDto(AdminActivityLog entry) => new(
        entry.Id,
        entry.ActorId,
        entry.ActorUsername,
        entry.ActorDisplayName,
        entry.Action,
        ActivityActions.TitleOf(entry.Action),
        entry.SectionId,
        entry.SectionTitle,
        entry.EntityLabel,
        entry.Description,
        entry.ChangedFieldList,
        entry.HttpMethod,
        entry.Path,
        entry.StatusCode,
        entry.Succeeded,
        entry.IpAddress,
        entry.UserAgent,
        entry.OccurredAt);

    /// <summary>Iran has used a fixed +03:30 offset since 2022 — no DST math needed.</summary>
    public static readonly TimeSpan IranOffset = TimeSpan.FromMinutes(210);

    public static DateTimeOffset LocalDayStart(DateOnly date) =>
        new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), IranOffset).ToUniversalTime();

    public static DateOnly TehranDateOf(DateTimeOffset moment) =>
        DateOnly.FromDateTime(moment.ToOffset(IranOffset).DateTime);
}
