using Microsoft.EntityFrameworkCore;

namespace Sugarista.Application.Maintenance.Commands.DeleteMaintenanceServiceLog;

public record DeleteMaintenanceServiceLogCommand(Guid EquipmentId, Guid Id) : IRequest;

public class DeleteMaintenanceServiceLogCommandValidator : AbstractValidator<DeleteMaintenanceServiceLogCommand>
{
    public DeleteMaintenanceServiceLogCommandValidator()
    {
        RuleFor(command => command.EquipmentId).NotEmpty();
        RuleFor(command => command.Id).NotEmpty();
    }
}

public class DeleteMaintenanceServiceLogCommandHandler : IRequestHandler<DeleteMaintenanceServiceLogCommand>
{
    private readonly IApplicationDbContext _dbContext;

    public DeleteMaintenanceServiceLogCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task Handle(DeleteMaintenanceServiceLogCommand request, CancellationToken cancellationToken)
    {
        var log = await _dbContext.MaintenanceServiceLogs
            .FirstOrDefaultAsync(item => item.Id == request.Id && item.EquipmentId == request.EquipmentId, cancellationToken)
            ?? throw new KeyNotFoundException("سابقه سرویس یافت نشد.");

        _dbContext.MaintenanceServiceLogs.Remove(log);

        // If the log closed a schedule cycle, recompute that schedule from what remains.
        if (log.ScheduleId is { } scheduleId)
        {
            var schedule = await _dbContext.MaintenanceSchedules
                .Include(schedule => schedule.Equipment)
                    .ThenInclude(equipment => equipment.ServiceLogs)
                .FirstOrDefaultAsync(schedule => schedule.Id == scheduleId, cancellationToken);

            if (schedule is not null)
            {
                var remainingForSchedule = schedule.Equipment.ServiceLogs
                    .Where(item => item.ScheduleId == scheduleId && item.Id != log.Id)
                    .Select(item => item.ServicedOn)
                    .ToList();

                if (remainingForSchedule.Count > 0)
                {
                    schedule.MarkServiced(remainingForSchedule.Max());
                }
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
