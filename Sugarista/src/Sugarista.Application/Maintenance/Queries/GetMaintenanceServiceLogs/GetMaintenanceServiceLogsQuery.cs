using Microsoft.EntityFrameworkCore;

namespace Sugarista.Application.Maintenance.Queries.GetMaintenanceServiceLogs;

public sealed record MaintenanceServiceLogDto(
    Guid Id,
    Guid EquipmentId,
    string EquipmentName,
    Guid? ScheduleId,
    string? ScheduleTitle,
    DateOnly ServicedOn,
    decimal Cost,
    string? PerformedBy,
    string? Description);

public record GetMaintenanceServiceLogsQuery(Guid? EquipmentId, int Take = 300)
    : IRequest<IReadOnlyList<MaintenanceServiceLogDto>>;

public class GetMaintenanceServiceLogsQueryHandler
    : IRequestHandler<GetMaintenanceServiceLogsQuery, IReadOnlyList<MaintenanceServiceLogDto>>
{
    private readonly IApplicationDbContext _dbContext;

    public GetMaintenanceServiceLogsQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<MaintenanceServiceLogDto>> Handle(
        GetMaintenanceServiceLogsQuery request,
        CancellationToken cancellationToken)
    {
        var take = Math.Clamp(request.Take, 1, 1000);

        var logsQuery = _dbContext.MaintenanceServiceLogs
            .AsNoTracking()
            .Include(log => log.Equipment)
            .Include(log => log.Schedule)
            .AsQueryable();

        if (request.EquipmentId is { } equipmentId)
        {
            logsQuery = logsQuery.Where(log => log.EquipmentId == equipmentId);
        }

        var logs = await logsQuery
            .OrderByDescending(log => log.ServicedOn)
            .ThenByDescending(log => log.CreatedAt)
            .Take(take)
            .Select(log => new MaintenanceServiceLogDto(
                log.Id,
                log.EquipmentId,
                log.Equipment.PersianName,
                log.ScheduleId,
                log.Schedule != null ? log.Schedule.Title : null,
                log.ServicedOn,
                log.Cost,
                log.PerformedBy,
                log.Description))
            .ToListAsync(cancellationToken);

        return logs;
    }
}
