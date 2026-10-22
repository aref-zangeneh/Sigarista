using Microsoft.EntityFrameworkCore;
using Sugarista.Application.ActivityLogs.Dto;
using Sugarista.Application.Common.Models;

namespace Sugarista.Application.ActivityLogs.GetActivityLogs;

public record GetActivityLogsQuery(
    string? Search = null,
    string? SectionId = null,
    string? Action = null,
    string? Username = null,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null,
    int Page = 1,
    int PageSize = 30) : IRequest<PaginatedList<AdminActivityLogDto>>;

public class GetActivityLogsQueryHandler
    : IRequestHandler<GetActivityLogsQuery, PaginatedList<AdminActivityLogDto>>
{
    private const int MaxPageSize = 100;

    private readonly IApplicationDbContext _dbContext;

    public GetActivityLogsQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PaginatedList<AdminActivityLogDto>> Handle(
        GetActivityLogsQuery request,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, MaxPageSize);

        var query = _dbContext.AdminActivityLogs
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(entry =>
                entry.Description.Contains(term) ||
                (entry.EntityLabel != null && entry.EntityLabel.Contains(term)) ||
                entry.ActorUsername.Contains(term) ||
                (entry.ActorDisplayName != null && entry.ActorDisplayName.Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(request.SectionId))
        {
            var sectionId = request.SectionId.Trim();
            query = query.Where(entry => entry.SectionId == sectionId);
        }

        if (!string.IsNullOrWhiteSpace(request.Action))
        {
            var action = request.Action.Trim();
            query = query.Where(entry => entry.Action == action);
        }

        if (!string.IsNullOrWhiteSpace(request.Username))
        {
            var username = request.Username.Trim();
            query = query.Where(entry => entry.ActorUsername == username);
        }

        if (request.FromDate.HasValue)
        {
            var from = ActivityLogMapper.LocalDayStart(request.FromDate.Value);
            query = query.Where(entry => entry.OccurredAt >= from);
        }

        if (request.ToDate.HasValue)
        {
            var to = ActivityLogMapper.LocalDayStart(request.ToDate.Value.AddDays(1));
            query = query.Where(entry => entry.OccurredAt < to);
        }

        var total = await query.CountAsync(cancellationToken);

        var rows = await query
            .OrderByDescending(entry => entry.OccurredAt)
            .ThenByDescending(entry => entry.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PaginatedList<AdminActivityLogDto>(
            rows.Select(ActivityLogMapper.ToDto).ToList(),
            total,
            page,
            pageSize);
    }
}
