using Microsoft.EntityFrameworkCore;
using Sugarista.Application.ActivityLogs.Dto;

namespace Sugarista.Application.ActivityLogs.GetActivityLogSummary;

public record GetActivityLogSummaryQuery : IRequest<ActivityLogSummaryDto>;

public class GetActivityLogSummaryQueryHandler
    : IRequestHandler<GetActivityLogSummaryQuery, ActivityLogSummaryDto>
{
    private readonly IApplicationDbContext _dbContext;

    public GetActivityLogSummaryQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ActivityLogSummaryDto> Handle(
        GetActivityLogSummaryQuery request,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var todayStart = ActivityLogMapper.LocalDayStart(ActivityLogMapper.TehranDateOf(now));
        var weekStart = todayStart.AddDays(-6);

        var totalEvents = await _dbContext.AdminActivityLogs
            .AsNoTracking()
            .CountAsync(cancellationToken);

        var todayQuery = _dbContext.AdminActivityLogs
            .AsNoTracking()
            .Where(entry => entry.OccurredAt >= todayStart);

        var todayEvents = await todayQuery.CountAsync(cancellationToken);
        var failedToday = await todayQuery.CountAsync(entry => !entry.Succeeded, cancellationToken);
        var loginsToday = await todayQuery.CountAsync(
            entry => entry.Action == ActivityActions.Login && entry.Succeeded,
            cancellationToken);

        var activeUsersInWeek = await _dbContext.AdminActivityLogs
            .AsNoTracking()
            .Where(entry => entry.OccurredAt >= weekStart)
            .Select(entry => entry.ActorUsername)
            .Distinct()
            .CountAsync(cancellationToken);

        var lastEventAt = await _dbContext.AdminActivityLogs
            .AsNoTracking()
            .OrderByDescending(entry => entry.OccurredAt)
            .Select(entry => (DateTimeOffset?)entry.OccurredAt)
            .FirstOrDefaultAsync(cancellationToken);

        return new ActivityLogSummaryDto(
            totalEvents,
            todayEvents,
            failedToday,
            loginsToday,
            activeUsersInWeek,
            lastEventAt);
    }
}
