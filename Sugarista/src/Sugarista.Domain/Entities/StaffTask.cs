using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

public enum StaffTaskPriority
{
    Normal = 0,
    High = 1,
    Critical = 2
}

public enum StaffTaskRecurrenceType
{
    Daily = 0,
    Weekly = 1,
    OneTime = 2
}

/// <summary>
/// A recurring or one-time operational task assigned to a specific staff member (<see cref="AdminUser"/>).
/// When <see cref="RaiseTimeOfDay"/> arrives (in Iran Standard Time, UTC+03:30), a persistent
/// non-dismissible toast stays on that staff member's admin panel until marked done.
/// </summary>
public class StaffTask : BaseAuditableEntity
{
    public static readonly TimeSpan TehranOffset = TimeSpan.FromHours(3.5);
    public const int AllDaysMask = 0b111_1111; // 127 (Sunday=1 .. Saturday=64)

    private StaffTask()
    {
    }

    public StaffTask(
        string title,
        string? description,
        Guid assignedToAdminUserId,
        string? category,
        StaffTaskPriority priority,
        StaffTaskRecurrenceType recurrenceType,
        TimeOnly raiseTimeOfDay,
        int daysOfWeekMask = AllDaysMask,
        DateOnly? oneTimeDate = null,
        bool requireNoteOnComplete = false,
        bool isActive = true)
    {
        Update(
            title,
            description,
            assignedToAdminUserId,
            category,
            priority,
            recurrenceType,
            raiseTimeOfDay,
            daysOfWeekMask,
            oneTimeDate,
            requireNoteOnComplete,
            isActive);
    }

    public string Title { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public Guid AssignedToAdminUserId { get; private set; }

    public AdminUser AssignedToAdminUser { get; private set; } = null!;

    public string Category { get; private set; } = "عملیات روزانه";

    public StaffTaskPriority Priority { get; private set; } = StaffTaskPriority.Normal;

    public StaffTaskRecurrenceType RecurrenceType { get; private set; } = StaffTaskRecurrenceType.Daily;

    /// <summary>
    /// Bitmask using standard <see cref="DayOfWeek"/> bits: <c>1 &lt;&lt; (int)DayOfWeek</c>
    /// (Sunday=1, Monday=2, Tuesday=4, Wednesday=8, Thursday=16, Friday=32, Saturday=64).
    /// </summary>
    public int DaysOfWeekMask { get; private set; } = AllDaysMask;

    /// <summary>Tehran calendar date when <see cref="RecurrenceType"/> is <see cref="StaffTaskRecurrenceType.OneTime"/>.</summary>
    public DateOnly? OneTimeDate { get; private set; }

    /// <summary>Local Tehran time-of-day (e.g. 21:00) at which this task raises as a persistent toast.</summary>
    public TimeOnly RaiseTimeOfDay { get; private set; }

    public bool RequireNoteOnComplete { get; private set; }

    public bool IsActive { get; private set; } = true;

    public ICollection<StaffTaskCompletion> Completions { get; private set; } = [];

    public void Update(
        string title,
        string? description,
        Guid assignedToAdminUserId,
        string? category,
        StaffTaskPriority priority,
        StaffTaskRecurrenceType recurrenceType,
        TimeOnly raiseTimeOfDay,
        int daysOfWeekMask,
        DateOnly? oneTimeDate,
        bool requireNoteOnComplete,
        bool isActive)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new DomainException("عنوان تسک الزامی است.");
        }

        if (assignedToAdminUserId == Guid.Empty)
        {
            throw new DomainException("مسئول انجام تسک باید انتخاب شود.");
        }

        if (recurrenceType == StaffTaskRecurrenceType.Weekly && (daysOfWeekMask & AllDaysMask) == 0)
        {
            throw new DomainException("برای تسک هفتگی حداقل یک روز هفته را انتخاب کنید.");
        }

        if (recurrenceType == StaffTaskRecurrenceType.OneTime && !oneTimeDate.HasValue)
        {
            throw new DomainException("برای تسک یک‌باره باید تاریخ اجرا مشخص شود.");
        }

        var trimmedTitle = title.Trim();
        Title = trimmedTitle.Length > 160 ? trimmedTitle[..160] : trimmedTitle;

        if (string.IsNullOrWhiteSpace(description))
        {
            Description = null;
        }
        else
        {
            var trimmedDesc = description.Trim();
            Description = trimmedDesc.Length > 800 ? trimmedDesc[..800] : trimmedDesc;
        }

        AssignedToAdminUserId = assignedToAdminUserId;

        var cleanCategory = string.IsNullOrWhiteSpace(category) ? "عملیات روزانه" : category.Trim();
        Category = cleanCategory.Length > 60 ? cleanCategory[..60] : cleanCategory;

        Priority = priority;
        RecurrenceType = recurrenceType;
        RaiseTimeOfDay = raiseTimeOfDay;
        DaysOfWeekMask = recurrenceType == StaffTaskRecurrenceType.Weekly
            ? (daysOfWeekMask & AllDaysMask)
            : AllDaysMask;
        OneTimeDate = recurrenceType == StaffTaskRecurrenceType.OneTime ? oneTimeDate : null;
        RequireNoteOnComplete = requireNoteOnComplete;
        IsActive = isActive;
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
    }

    public static DateOnly ToTehranDate(DateTimeOffset utcNow)
    {
        var tehran = utcNow.ToOffset(TehranOffset);
        return DateOnly.FromDateTime(tehran.DateTime);
    }

    public DateTimeOffset GetScheduledAtUtc(DateOnly tehranDate)
    {
        var local = new DateTimeOffset(
            tehranDate.Year,
            tehranDate.Month,
            tehranDate.Day,
            RaiseTimeOfDay.Hour,
            RaiseTimeOfDay.Minute,
            RaiseTimeOfDay.Second,
            TehranOffset);
        return local.ToUniversalTime();
    }

    public bool IsScheduledForTehranDate(DateOnly tehranDate)
    {
        return RecurrenceType switch
        {
            StaffTaskRecurrenceType.Daily => true,
            StaffTaskRecurrenceType.Weekly => (DaysOfWeekMask & (1 << (int)tehranDate.DayOfWeek)) != 0,
            StaffTaskRecurrenceType.OneTime => OneTimeDate.HasValue && OneTimeDate.Value == tehranDate,
            _ => false
        };
    }

    /// <summary>
    /// Returns the occurrence date for which this task should currently be raised at <paramref name="nowUtc"/>,
    /// or <c>null</c> if the task is inactive or not yet due.
    /// For one-time tasks, if the scheduled time has already passed (even on an earlier date),
    /// it continues to return <see cref="OneTimeDate"/> until completed.
    /// </summary>
    public DateOnly? GetDueOccurrenceDateAt(DateTimeOffset nowUtc)
    {
        if (!IsActive)
        {
            return null;
        }

        if (RecurrenceType == StaffTaskRecurrenceType.OneTime)
        {
            if (!OneTimeDate.HasValue)
            {
                return null;
            }

            var scheduledOneTime = GetScheduledAtUtc(OneTimeDate.Value);
            return nowUtc >= scheduledOneTime ? OneTimeDate.Value : null;
        }

        var tehranToday = ToTehranDate(nowUtc);
        if (!IsScheduledForTehranDate(tehranToday))
        {
            return null;
        }

        var scheduledToday = GetScheduledAtUtc(tehranToday);
        return nowUtc >= scheduledToday ? tehranToday : null;
    }
}

/// <summary>
/// Records the completion of one scheduled occurrence of a <see cref="StaffTask"/>.
/// Enforces a unique (StaffTaskId, OccurrenceDate) constraint so each occurrence is completed once cleanly.
/// </summary>
public class StaffTaskCompletion : BaseEntity
{
    private StaffTaskCompletion()
    {
    }

    public StaffTaskCompletion(
        Guid staffTaskId,
        DateOnly occurrenceDate,
        DateTimeOffset scheduledAtUtc,
        DateTimeOffset completedAtUtc,
        Guid completedByAdminUserId,
        string completedByDisplayName,
        string? note = null)
    {
        if (staffTaskId == Guid.Empty)
        {
            throw new DomainException("شناسه تسک الزامی است.");
        }

        StaffTaskId = staffTaskId;
        OccurrenceDate = occurrenceDate;
        ScheduledAtUtc = scheduledAtUtc.ToUniversalTime();
        CompletedAtUtc = completedAtUtc.ToUniversalTime();
        CompletedByAdminUserId = completedByAdminUserId;
        var cleanName = string.IsNullOrWhiteSpace(completedByDisplayName) ? "کاربر" : completedByDisplayName.Trim();
        CompletedByDisplayName = cleanName.Length > 120 ? cleanName[..120] : cleanName;
        DelayMinutes = Math.Max(0, (int)Math.Floor((CompletedAtUtc - ScheduledAtUtc).TotalMinutes));

        if (!string.IsNullOrWhiteSpace(note))
        {
            var trimmed = note.Trim();
            Note = trimmed.Length > 500 ? trimmed[..500] : trimmed;
        }
    }

    public Guid StaffTaskId { get; private set; }

    public StaffTask StaffTask { get; private set; } = null!;

    public DateOnly OccurrenceDate { get; private set; }

    public DateTimeOffset ScheduledAtUtc { get; private set; }

    public DateTimeOffset CompletedAtUtc { get; private set; }

    public Guid CompletedByAdminUserId { get; private set; }

    public string CompletedByDisplayName { get; private set; } = string.Empty;

    public int DelayMinutes { get; private set; }

    public string? Note { get; private set; }
}

/// <summary>
/// Immutable audit trail for every staff-task lifecycle event (Created, Updated, Activated,
/// Deactivated, Deleted, Completed, Reopened). Survives even after task deletion.
/// </summary>
public class StaffTaskActivityLog : BaseEntity
{
    private StaffTaskActivityLog()
    {
    }

    public StaffTaskActivityLog(
        Guid? staffTaskId,
        string taskTitleSnapshot,
        string assignedToDisplayName,
        string action,
        Guid? actorAdminUserId,
        string actorDisplayName,
        string? details,
        DateTimeOffset occurredAtUtc)
    {
        StaffTaskId = staffTaskId;
        var title = string.IsNullOrWhiteSpace(taskTitleSnapshot) ? "تسک" : taskTitleSnapshot.Trim();
        TaskTitleSnapshot = title.Length > 180 ? title[..180] : title;

        var assignee = string.IsNullOrWhiteSpace(assignedToDisplayName) ? "—" : assignedToDisplayName.Trim();
        AssignedToDisplayName = assignee.Length > 120 ? assignee[..120] : assignee;

        var cleanAction = string.IsNullOrWhiteSpace(action) ? "Updated" : action.Trim();
        Action = cleanAction.Length > 40 ? cleanAction[..40] : cleanAction;

        ActorAdminUserId = actorAdminUserId;
        var actor = string.IsNullOrWhiteSpace(actorDisplayName) ? "سیستم" : actorDisplayName.Trim();
        ActorDisplayName = actor.Length > 120 ? actor[..120] : actor;

        if (!string.IsNullOrWhiteSpace(details))
        {
            var d = details.Trim();
            Details = d.Length > 600 ? d[..600] : d;
        }

        OccurredAtUtc = occurredAtUtc.ToUniversalTime();
    }

    public Guid? StaffTaskId { get; private set; }

    public string TaskTitleSnapshot { get; private set; } = string.Empty;

    public string AssignedToDisplayName { get; private set; } = string.Empty;

    public string Action { get; private set; } = string.Empty;

    public Guid? ActorAdminUserId { get; private set; }

    public string ActorDisplayName { get; private set; } = string.Empty;

    public string? Details { get; private set; }

    public DateTimeOffset OccurredAtUtc { get; private set; }
}
