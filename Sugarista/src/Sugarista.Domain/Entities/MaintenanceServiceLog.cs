using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

/// <summary>
/// One performed service/repair entry on an equipment unit (سرویس و تعمیرات) —
/// with its cost so the total upkeep spend is always visible.
/// </summary>
public class MaintenanceServiceLog : BaseAuditableEntity
{
    private MaintenanceServiceLog()
    {
    }

    public MaintenanceServiceLog(
        Guid equipmentId,
        Guid? scheduleId,
        DateOnly servicedOn,
        decimal cost,
        string? performedBy,
        string? description)
    {
        if (equipmentId == Guid.Empty)
        {
            throw new DomainException("تجهیز نامعتبر است.");
        }

        if (cost < 0)
        {
            throw new DomainException("هزینه سرویس نمی‌تواند منفی باشد.");
        }

        EquipmentId = equipmentId;
        ScheduleId = scheduleId;
        ServicedOn = servicedOn;
        Cost = cost;

        PerformedBy = string.IsNullOrWhiteSpace(performedBy) ? null : performedBy.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }

    public Guid EquipmentId { get; private set; }

    public MaintenanceEquipment Equipment { get; private set; } = null!;

    /// <summary>Optional link to the schedule this service closes a cycle of.</summary>
    public Guid? ScheduleId { get; private set; }

    public MaintenanceSchedule? Schedule { get; private set; }

    public DateOnly ServicedOn { get; private set; }

    /// <summary>Cost of the service/repair (تومان).</summary>
    public decimal Cost { get; private set; }

    public string? PerformedBy { get; private set; }

    public string? Description { get; private set; }
}
