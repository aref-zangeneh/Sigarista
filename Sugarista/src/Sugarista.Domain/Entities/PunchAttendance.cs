using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

/// <summary>
/// Real presence record of one employee for one café day, captured with the
/// Punch-in / Punch-out buttons (حضور واقعی). Punch times are stored as UTC
/// instants so the café day keeps a definite Tehran-local date.
/// </summary>
public class PunchAttendance : BaseAuditableEntity
{
    private PunchAttendance()
    {
    }

    public PunchAttendance(
        Guid employeeId,
        DateTimeOffset punchInAt,
        DateOnly workDate,
        string source,
        string? note)
    {
        if (employeeId == Guid.Empty)
        {
            throw new DomainException("Employee is required.");
        }

        EmployeeId = employeeId;
        PunchInAt = punchInAt;
        WorkDate = workDate;
        Source = string.IsNullOrWhiteSpace(source) ? "dashboard" : source.Trim();
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }

    public Guid EmployeeId { get; private set; }

    public Employee? Employee { get; private set; }

    /// <summary>Café-local day of this punch pair (Tehran time).</summary>
    public DateOnly WorkDate { get; private set; }

    public DateTimeOffset? PunchInAt { get; private set; }

    public DateTimeOffset? PunchOutAt { get; private set; }

    public string Source { get; private set; } = "dashboard";

    public string? Note { get; private set; }

    public bool IsOpen => PunchInAt.HasValue && !PunchOutAt.HasValue;

    /// <summary>Worked minutes — an open (unclosed) pair contributes nothing.</summary>
    public decimal WorkedMinutes => PunchInAt.HasValue && PunchOutAt.HasValue
        ? (decimal)(PunchOutAt.Value - PunchInAt.Value).TotalMinutes
        : 0m;

    /// <summary>Opens the record — punch-in.</summary>
    public void PunchIn(DateTimeOffset now, DateOnly workDate, string? note)
    {
        if (IsOpen)
        {
            throw new DomainException("برای این پرسنل یک پانچ باز وجود دارد؛ ابتدا خروج را ثبت کنید.");
        }

        if (PunchInAt.HasValue)
        {
            throw new DomainException("برای این روز قبلاً ورود ثبت شده است.");
        }

        PunchInAt = now;
        WorkDate = workDate;
        if (!string.IsNullOrWhiteSpace(note))
        {
            Note = note.Trim();
        }
    }

    /// <summary>Closes the pair — punch-out (added later on purpose for the same day).</summary>
    public void PunchOut(DateTimeOffset now)
    {
        if (!IsOpen)
        {
            throw new DomainException("پانچ بازی برای خروج وجود ندارد.");
        }

        if (now <= PunchInAt!.Value)
        {
            throw new DomainException("ساعت خروج باید بعد از ساعت ورود باشد.");
        }

        PunchOutAt = now;
    }

    public void SetNote(string? note)
    {
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }
}
