using MediatR;
using Microsoft.EntityFrameworkCore;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Sms;

public sealed record SmsDashboardStatsDto(
    int TotalMessages,
    int SentCount,
    int FailedCount,
    int PendingCount,
    int DeliveredCount,
    int UndeliveredCount,
    decimal TotalCost,
    int ContactCount,
    int GroupCount,
    decimal? ActiveProviderCredit,
    string? ActiveProviderName);

public sealed record SmsDashboardPointDto(
    string DateLabel,
    int Total,
    int Sent,
    int Failed);

public sealed record SmsDashboardProviderDto(
    Guid? ProviderId,
    string ProviderName,
    int Total,
    int Sent,
    int Failed,
    decimal Cost);

public sealed record SmsDashboardKindDto(string Kind, int Count);

public sealed record SmsDashboardDto(
    SmsDashboardStatsDto Stats,
    IReadOnlyList<SmsDashboardPointDto> Timeline,
    IReadOnlyList<SmsDashboardProviderDto> Providers,
    IReadOnlyList<SmsDashboardKindDto> Kinds);

/// <summary>
/// Dashboard data comes ONLY from stored SmsMessage rows — no live provider
/// calls (except the active provider's credit, which the panel can refresh
/// separately). Range/Provider/Kind filters supported.
/// </summary>
public record GetSmsDashboardQuery(
    string Range = "7d",
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    Guid? ProviderId = null,
    string? Kind = null) : IRequest<SmsDashboardDto>;

public class GetSmsDashboardQueryHandler(IApplicationDbContext dbContext)
    : IRequestHandler<GetSmsDashboardQuery, SmsDashboardDto>
{
    public async Task<SmsDashboardDto> Handle(GetSmsDashboardQuery request, CancellationToken cancellationToken)
    {
        var (from, to, bucket) = ResolveRange(request);

        var messages = dbContext.SmsMessages
            .AsNoTracking()
            .Where(message => message.RequestedAt >= from && message.RequestedAt <= to);

        if (request.ProviderId is { } providerId)
        {
            messages = messages.Where(message => message.ProviderId == providerId);
        }

        if (!string.IsNullOrWhiteSpace(request.Kind)
            && Enum.TryParse<SmsMessageKind>(request.Kind, ignoreCase: true, out var kind))
        {
            messages = messages.Where(message => message.Kind == kind);
        }

        var stats = await messages.GroupBy(message => message.Status)
            .Select(group => new { Status = group.Key, Count = group.Count(), Cost = group.Sum(message => message.Cost ?? 0) })
            .ToListAsync(cancellationToken);

        var totalMessages = stats.Sum(entry => entry.Count);
        var sent = stats
            .Where(entry => entry.Status is SmsSendStatus.Accepted or SmsSendStatus.Sent or SmsSendStatus.Delivered)
            .Sum(entry => entry.Count);
        var failed = stats
            .Where(entry => entry.Status is SmsSendStatus.Failed or SmsSendStatus.Rejected)
            .Sum(entry => entry.Count);
        var pending = stats
            .Where(entry => entry.Status is SmsSendStatus.Pending or SmsSendStatus.Queued or SmsSendStatus.Processing)
            .Sum(entry => entry.Count);
        var delivered = stats.Where(entry => entry.Status == SmsSendStatus.Delivered).Sum(entry => entry.Count);
        var totalCost = stats.Sum(entry => entry.Cost);

        var contactCount = await dbContext.SmsContacts.CountAsync(cancellationToken);
        var groupCount = await dbContext.SmsContactGroups.CountAsync(cancellationToken);

        var activeProviderName = await dbContext.SmsProviders
            .AsNoTracking()
            .Where(provider => provider.IsEnabled && provider.IsDefault)
            .Select(provider => provider.Name)
            .FirstOrDefaultAsync(cancellationToken);

        // Timeline buckets.
        var timelineRaw = await messages
            .GroupBy(message => new
            {
                Year = message.RequestedAt.Year,
                Month = message.RequestedAt.Month,
                Day = message.RequestedAt.Day
            })
            .Select(group => new
            {
                group.Key.Year,
                group.Key.Month,
                group.Key.Day,
                Total = group.Count(),
                Sent = group.Count(message => message.Status == SmsSendStatus.Accepted
                                              || message.Status == SmsSendStatus.Sent
                                              || message.Status == SmsSendStatus.Delivered),
                Failed = group.Count(message => message.Status == SmsSendStatus.Failed
                                                || message.Status == SmsSendStatus.Rejected)
            })
            .ToListAsync(cancellationToken);

        var timeline = timelineRaw
            .OrderBy(entry => entry.Year)
            .ThenBy(entry => entry.Month)
            .ThenBy(entry => entry.Day)
            .Select(entry => new SmsDashboardPointDto(
                $"{entry.Year:0000}-{entry.Month:00}-{entry.Day:00}",
                entry.Total,
                entry.Sent,
                entry.Failed))
            .ToList();

        var providerStats = await messages
            .GroupBy(message => message.ProviderId)
            .Select(group => new
            {
                ProviderId = group.Key,
                Total = group.Count(),
                Sent = group.Count(message => message.Status == SmsSendStatus.Accepted
                                              || message.Status == SmsSendStatus.Sent
                                              || message.Status == SmsSendStatus.Delivered),
                Failed = group.Count(message => message.Status == SmsSendStatus.Failed
                                                || message.Status == SmsSendStatus.Rejected),
                Cost = group.Sum(message => message.Cost ?? 0)
            })
            .ToListAsync(cancellationToken);

        var providerIds = providerStats.Where(entry => entry.ProviderId is not null)
            .Select(entry => entry.ProviderId!.Value).ToList();
        var providerNames = providerIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await dbContext.SmsProviders
                .AsNoTracking()
                .Where(provider => providerIds.Contains(provider.Id))
                .ToDictionaryAsync(provider => provider.Id, provider => provider.Name, cancellationToken);

        var kindStats = await messages
            .GroupBy(message => message.Kind)
            .Select(group => new { Kind = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        return new SmsDashboardDto(
            new SmsDashboardStatsDto(
                totalMessages,
                sent,
                failed,
                pending,
                delivered,
                totalMessages - delivered,
                totalCost,
                contactCount,
                groupCount,
                null,
                activeProviderName),
            timeline,
            providerStats
                .Select(entry => new SmsDashboardProviderDto(
                    entry.ProviderId,
                    entry.ProviderId is { } id ? providerNames.GetValueOrDefault(id, "—") : "بدون Provider",
                    entry.Total,
                    entry.Sent,
                    entry.Failed,
                    entry.Cost))
                .OrderByDescending(entry => entry.Total)
                .ToList(),
            kindStats
                .Select(entry => new SmsDashboardKindDto(entry.Kind.ToString(), entry.Count))
                .OrderByDescending(entry => entry.Count)
                .ToList());
    }

    private static (DateTimeOffset From, DateTimeOffset To, string Bucket) ResolveRange(GetSmsDashboardQuery request)
    {
        var now = DateTimeOffset.UtcNow;
        var today = new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero); // UTC midnight
        return request.Range switch
        {
            "today" => (today, now, "day"),
            "yesterday" => (today.AddDays(-1), today, "day"),
            "7d" => (today.AddDays(-6), now, "day"),
            "30d" => (today.AddDays(-29), now, "day"),
            "custom" when request.From is { } from && request.To is { } to => (from, to, "day"),
            _ => (today.AddDays(-6), now, "day")
        };
    }
}
