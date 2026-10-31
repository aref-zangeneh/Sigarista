using Microsoft.EntityFrameworkCore;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Maintenance.Commands.UpsertMaintenanceSchedule;

public class UpsertMaintenanceScheduleCommandHandler : IRequestHandler<UpsertMaintenanceScheduleCommand, Guid>
{
    private readonly IApplicationDbContext _dbContext;

    public UpsertMaintenanceScheduleCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Guid> Handle(UpsertMaintenanceScheduleCommand request, CancellationToken cancellationToken)
    {
        var equipmentExists = await _dbContext.MaintenanceEquipments
            .AsNoTracking()
            .AnyAsync(equipment => equipment.Id == request.EquipmentId, cancellationToken);
        if (!equipmentExists)
        {
            throw new KeyNotFoundException("تجهیز یافت نشد.");
        }

        var recipients = ParseRecipients(request.EquipmentId, request.Recipients);

        if (request.Id is { } scheduleId)
        {
            var existing = await _dbContext.MaintenanceSchedules
                .Include(schedule => schedule.Recipients)
                .FirstOrDefaultAsync(schedule => schedule.Id == scheduleId && schedule.EquipmentId == request.EquipmentId, cancellationToken)
                ?? throw new KeyNotFoundException("زمان‌بندی سرویس یافت نشد.");

            existing.Update(
                request.Title,
                request.IntervalDays,
                request.NextDueDate,
                request.IsActive);

            existing.ReplaceRecipients(recipients);

            await _dbContext.SaveChangesAsync(cancellationToken);
            return existing.Id;
        }

        var schedule = new MaintenanceSchedule(
            request.EquipmentId,
            request.Title,
            request.IntervalDays,
            request.NextDueDate);

        if (!request.IsActive)
        {
            schedule.Deactivate();
        }

        _dbContext.MaintenanceSchedules.Add(schedule);
        schedule.ReplaceRecipients(recipients);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return schedule.Id;
    }

    private static List<(MaintenanceChannel Channel, string Target, string? Label)> ParseRecipients(
        Guid equipmentId,
        IReadOnlyList<MaintenanceRecipientInput> inputs)
    {
        var recipients = new List<(MaintenanceChannel, string, string?)>();

        foreach (var input in inputs)
        {
            if (!Enum.TryParse<MaintenanceChannel>(input.Channel, ignoreCase: true, out var channel)
                || channel is not (MaintenanceChannel.Sms or MaintenanceChannel.Email))
            {
                throw new InvalidOperationException($"کانال یادآوری «{input.Channel}» معتبر نیست. از Sms یا Email استفاده کنید.");
            }

            recipients.Add((channel, input.Target.Trim(), input.Label));
        }

        return recipients
            .GroupBy(item => $"{item.Item1}:{item.Item2.Trim()}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }
}
