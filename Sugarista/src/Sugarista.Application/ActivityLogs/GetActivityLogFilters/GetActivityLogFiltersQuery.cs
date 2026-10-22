using Microsoft.EntityFrameworkCore;
using Sugarista.Application.ActivityLogs.Dto;

namespace Sugarista.Application.ActivityLogs.GetActivityLogFilters;

/// <summary>Distinct actors/sections used to build the log filter dropdowns.</summary>
public record GetActivityLogFiltersQuery : IRequest<ActivityLogFiltersDto>;

public class GetActivityLogFiltersQueryHandler
    : IRequestHandler<GetActivityLogFiltersQuery, ActivityLogFiltersDto>
{
    private readonly IApplicationDbContext _dbContext;

    public GetActivityLogFiltersQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ActivityLogFiltersDto> Handle(
        GetActivityLogFiltersQuery request,
        CancellationToken cancellationToken)
    {
        var actorRows = await _dbContext.AdminActivityLogs
            .AsNoTracking()
            .GroupBy(entry => new { entry.ActorUsername, entry.ActorDisplayName })
            .Select(group => new
            {
                group.Key.ActorUsername,
                group.Key.ActorDisplayName,
                Count = group.Count(),
            })
            .OrderByDescending(item => item.Count)
            .Take(50)
            .ToListAsync(cancellationToken);

        var sectionRows = await _dbContext.AdminActivityLogs
            .AsNoTracking()
            .GroupBy(entry => new { entry.SectionId, entry.SectionTitle })
            .Select(group => new
            {
                group.Key.SectionId,
                group.Key.SectionTitle,
                Count = group.Count(),
            })
            .OrderByDescending(item => item.Count)
            .Take(50)
            .ToListAsync(cancellationToken);

        return new ActivityLogFiltersDto(
            actorRows
                .Select(item => new ActivityLogActorFilterDto(
                    item.ActorUsername,
                    item.ActorDisplayName,
                    item.Count))
                .ToList(),
            sectionRows
                .Select(item => new ActivityLogSectionFilterDto(
                    item.SectionId,
                    item.SectionTitle,
                    item.Count))
                .ToList(),
            ActivityActions.All);
    }
}
