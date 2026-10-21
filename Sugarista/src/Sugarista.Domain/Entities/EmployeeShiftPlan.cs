using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

/// <summary>
/// Approved weekly shift of one employee for one weekday (شیفت مصوب).
/// The payroll engine compares the real presence (punch in/out) against
/// these rows to derive overtime and short-work deductions.
/// </summary>
public class EmployeeShiftPlan : BaseAuditableEntity
{
    private EmployeeShiftPlan()
    {
    }

    public EmployeeShiftPlan(Guid employeeId, int weekday, TimeOnly startTime, TimeOnly endTime)
    {
        if (employeeId == Guid.Empty)
        {
            throw new DomainException("Employee is required.");
        }

        if (weekday is < 0 or > 6)
        {
            throw new DomainException("روز هفته نامعتبر است.");
        }

        Validate(startTime, endTime);

        EmployeeId = employeeId;
        Weekday = weekday;
        StartTime = startTime;
        EndTime = endTime;
    }

    public Guid EmployeeId { get; private set; }

    public Employee? Employee { get; private set; }

    public int Weekday { get; private set; }

    public TimeOnly StartTime { get; private set; }

    public TimeOnly EndTime { get; private set; }

    /// <summary>Approved shift length in minutes.</summary>
    public decimal ScheduledMinutes => (decimal)(EndTime.ToTimeSpan() - StartTime.ToTimeSpan()).TotalMinutes;

    public void Update(int weekday, TimeOnly startTime, TimeOnly endTime)
    {
        if (weekday is < 0 or > 6)
        {
            throw new DomainException("روز هفته نامعتبر است.");
        }

        Validate(startTime, endTime);
        Weekday = weekday;
        StartTime = startTime;
        EndTime = endTime;
    }

    private static void Validate(TimeOnly startTime, TimeOnly endTime)
    {
        if (endTime <= startTime)
        {
            throw new DomainException("ساعت پایان شیفت باید بعد از ساعت شروع باشد.");
        }
    }
}
