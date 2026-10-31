using Microsoft.EntityFrameworkCore;

namespace Sugarista.Application.Maintenance.Commands.DeleteMaintenanceSchedule;

public record DeleteMaintenanceScheduleCommand(Guid EquipmentId, Guid Id) : IRequest;

public class DeleteMaintenanceScheduleCommandValidator : AbstractValidator<DeleteMaintenanceScheduleCommand>
{
    public DeleteMaintenanceScheduleCommandValidator()
    {
        RuleFor(command => command.EquipmentId).NotEmpty();
        RuleFor(command => command.Id).NotEmpty();
    }
}

public class DeleteMaintenanceScheduleCommandHandler : IRequestHandler<DeleteMaintenanceScheduleCommand>
{
    private readonly IApplicationDbContext _dbContext;

    public DeleteMaintenanceScheduleCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task Handle(DeleteMaintenanceScheduleCommand request, CancellationToken cancellationToken)
    {
        var schedule = await _dbContext.MaintenanceSchedules
            .FirstOrDefaultAsync(
                schedule => schedule.Id == request.Id && schedule.EquipmentId == request.EquipmentId,
                cancellationToken)
            ?? throw new KeyNotFoundException("زمان‌بندی سرویس یافت نشد.");

        _dbContext.MaintenanceSchedules.Remove(schedule);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
