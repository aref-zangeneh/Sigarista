using Microsoft.EntityFrameworkCore;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Maintenance.Commands.CreateMaintenanceServiceLog;

public record CreateMaintenanceServiceLogCommand(
    Guid EquipmentId,
    Guid? ScheduleId,
    DateOnly ServicedOn,
    decimal Cost,
    string? PerformedBy,
    string? Description,
    bool CloseScheduleCycle = true) : IRequest<Guid>;

public class CreateMaintenanceServiceLogCommandValidator : AbstractValidator<CreateMaintenanceServiceLogCommand>
{
    public CreateMaintenanceServiceLogCommandValidator()
    {
        RuleFor(command => command.EquipmentId).NotEmpty();
        RuleFor(command => command.Cost)
            .GreaterThanOrEqualTo(0).WithMessage("هزینه سرویس نمی‌تواند منفی باشد.");
        RuleFor(command => command.PerformedBy).MaximumLength(120);
        RuleFor(command => command.Description).MaximumLength(600);
    }
}

public class CreateMaintenanceServiceLogCommandHandler : IRequestHandler<CreateMaintenanceServiceLogCommand, Guid>
{
    private readonly IApplicationDbContext _dbContext;

    public CreateMaintenanceServiceLogCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Guid> Handle(CreateMaintenanceServiceLogCommand request, CancellationToken cancellationToken)
    {
        var equipment = await _dbContext.MaintenanceEquipments
            .FirstOrDefaultAsync(item => item.Id == request.EquipmentId, cancellationToken)
            ?? throw new KeyNotFoundException("تجهیز یافت نشد.");

        MaintenanceSchedule? schedule = null;
        if (request.ScheduleId is { } scheduleId)
        {
            schedule = await _dbContext.MaintenanceSchedules
                .FirstOrDefaultAsync(
                    item => item.Id == scheduleId && item.EquipmentId == request.EquipmentId,
                    cancellationToken)
                ?? throw new KeyNotFoundException("زمان‌بندی سرویس یافت نشد.");
        }

        var log = new MaintenanceServiceLog(
            equipment.Id,
            schedule?.Id,
            request.ServicedOn,
            request.Cost,
            request.PerformedBy,
            request.Description);

        if (schedule is not null && request.CloseScheduleCycle)
        {
            schedule.MarkServiced(request.ServicedOn);
        }

        _dbContext.MaintenanceServiceLogs.Add(log);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return log.Id;
    }
}
