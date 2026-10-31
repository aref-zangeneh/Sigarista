using Microsoft.EntityFrameworkCore;

namespace Sugarista.Application.Maintenance.Queries.GetMaintenanceEquipments;

public sealed record MaintenanceRecipientDto(
    Guid Id,
    string Channel,
    string Target,
    string? Label);

public sealed record MaintenanceScheduleDto(
    Guid Id,
    Guid EquipmentId,
    string Title,
    int IntervalDays,
    DateOnly NextDueDate,
    DateOnly? LastCompletedOn,
    bool IsActive,
    int DaysRemaining,
    bool IsOverdue,
    IReadOnlyList<MaintenanceRecipientDto> Recipients);

public sealed record MaintenanceEquipmentDto(
    Guid Id,
    string PersianName,
    string? Category,
    string? BrandModel,
    string? SerialNumber,
    string? Location,
    DateOnly? PurchaseDate,
    string? Notes,
    bool IsActive,
    decimal TotalServiceCost,
    DateOnly? LastServiceDate,
    int ServiceLogCount,
    IReadOnlyList<MaintenanceScheduleDto> Schedules);

public record GetMaintenanceEquipmentsQuery(bool IncludeInactive = true)
    : IRequest<IReadOnlyList<MaintenanceEquipmentDto>>;

public class GetMaintenanceEquipmentsQueryHandler
    : IRequestHandler<GetMaintenanceEquipmentsQuery, IReadOnlyList<MaintenanceEquipmentDto>>
{
    private readonly IApplicationDbContext _dbContext;

    public GetMaintenanceEquipmentsQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<MaintenanceEquipmentDto>> Handle(
        GetMaintenanceEquipmentsQuery request,
        CancellationToken cancellationToken)
    {
        var today = MaintenanceReminderDispatcher.TehranToday();

        var equipments = await _dbContext.MaintenanceEquipments
            .AsNoTracking()
            .Include(equipment => equipment.Schedules)
                .ThenInclude(schedule => schedule.Recipients)
            .Where(equipment => request.IncludeInactive || equipment.IsActive)
            .OrderBy(equipment => equipment.PersianName)
            .ToListAsync(cancellationToken);

        var stats = await _dbContext.MaintenanceServiceLogs
            .AsNoTracking()
            .GroupBy(log => log.EquipmentId)
            .Select(group => new
            {
                EquipmentId = group.Key,
                TotalCost = group.Sum(log => log.Cost),
                LastServiceDate = (DateOnly?)group.Max(log => log.ServicedOn),
                Count = group.Count(),
            })
            .ToListAsync(cancellationToken);

        var statsById = stats.ToDictionary(stat => stat.EquipmentId);

        var result = equipments.Select(equipment =>
        {
            var scheduledDtos = equipment.Schedules
                .OrderBy(schedule => schedule.NextDueDate)
                .Select(schedule =>
                {
                    var daysRemaining = schedule.NextDueDate.DayNumber - today.DayNumber;
                    return new MaintenanceScheduleDto(
                        schedule.Id,
                        schedule.EquipmentId,
                        schedule.Title,
                        schedule.IntervalDays,
                        schedule.NextDueDate,
                        schedule.LastCompletedOn,
                        schedule.IsActive,
                        daysRemaining,
                        schedule.IsActive && daysRemaining <= 0,
                        schedule.Recipients
                            .OrderBy(recipient => recipient.Channel)
                            .ThenBy(recipient => recipient.Target, StringComparer.Ordinal)
                            .Select(recipient => new MaintenanceRecipientDto(
                                recipient.Id,
                                recipient.Channel.ToString(),
                                recipient.Target,
                                recipient.Label))
                            .ToList());
                })
                .ToList();

            statsById.TryGetValue(equipment.Id, out var stat);

            return new MaintenanceEquipmentDto(
                equipment.Id,
                equipment.PersianName,
                equipment.Category,
                equipment.BrandModel,
                equipment.SerialNumber,
                equipment.Location,
                equipment.PurchaseDate,
                equipment.Notes,
                equipment.IsActive,
                stat?.TotalCost ?? 0m,
                stat?.LastServiceDate,
                stat?.Count ?? 0,
                scheduledDtos);
        }).ToList();

        return result;
    }
}
