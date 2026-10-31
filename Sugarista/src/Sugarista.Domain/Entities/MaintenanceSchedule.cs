using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

/// <summary>
/// One recurring service reminder for an equipment unit: «هر {IntervalDays} روز
///Prefers یادآوری». Holds its own recipient list (dynamic emails / phone
/// numbers) and the next due date that the reminder ticker works through.
/// </summary>
public class MaintenanceSchedule : BaseAuditableEntity
{
    private MaintenanceSchedule()
    {
    }

    public MaintenanceSchedule(
        Guid equipmentId,
        string title,
        int intervalDays,
        DateOnly nextDueDate)
    {
        SetTitle(title);
        SetIntervalDays(intervalDays);

        EquipmentId = equipmentId;
        NextDueDate = nextDueDate;
        IsActive = true;
        Recipients = [];
    }

    public Guid EquipmentId { get; private set; }

    public MaintenanceEquipment Equipment { get; private set; } = null!;

    /// <summary>What must be done on every cycle (روغن‌کاری، دزیل، فیلتر، ...).</summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>Reminder period in days (from one service/reminder to the next).</summary>
    public int IntervalDays { get; private set; }

    /// <summary>The date this cycle's reminder targets (Tehran calendar).</summary>
    public DateOnly NextDueDate { get; private set; }

    /// <summary>Most recent completed service date, if any log is linked.</summary>
    public DateOnly? LastCompletedOn { get; private set; }

    public bool IsActive { get; private set; }

    public ICollection<MaintenanceRecipient> Recipients { get; private set; } = [];

    public void Update(
        string title,
        int intervalDays,
        DateOnly nextDueDate,
        bool isActive)
    {
        SetTitle(title);
        SetIntervalDays(intervalDays);

        NextDueDate = nextDueDate;
        IsActive = isActive;
    }

    /// <summary>Called when a verified service/repair log lands on this schedule.</summary>
    public void MarkServiced(DateOnly servicedOn)
    {
        NextDueDate = servicedOn.AddDays(IntervalDays);
        LastCompletedOn = servicedOn;
    }

    /// <summary>Once a reminder fires, jump to the next cycle.</summary>
    public void AdvanceToNextCycle()
    {
        NextDueDate = NextDueDate.AddDays(IntervalDays);
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    /// <summary>Swaps the whole recipient list in one go (fully dynamic).</summary>
    public void ReplaceRecipients(IReadOnlyCollection<(MaintenanceChannel Channel, string Target, string? Label)> recipients)
    {
        Recipients.Clear();
        foreach (var (channel, target, label) in recipients)
        {
            Recipients.Add(new MaintenanceRecipient(Id, channel, target, label));
        }
    }

    private void SetTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new DomainException("عنوان زمان‌بندی سرویس الزامی است.");
        }

        Title = title.Trim();
    }

    private void SetIntervalDays(int intervalDays)
    {
        if (intervalDays < 1 || intervalDays > 3650)
        {
            throw new DomainException("فاصله سرویس باید بین ۱ تا ۳۶۵۰ روز باشد.");
        }

        IntervalDays = intervalDays;
    }
}
