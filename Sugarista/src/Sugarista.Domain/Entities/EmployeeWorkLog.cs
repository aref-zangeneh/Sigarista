using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

/// <summary>
/// One day's recorded work for a staff member (حضور و کارکرد):
/// worked hours, overtime hours and shift count — the raw data of payroll.
/// </summary>
public class EmployeeWorkLog : BaseAuditableEntity
{
    private EmployeeWorkLog()
    {
    }

    public EmployeeWorkLog(
        Guid employeeId,
        DateOnly workDate,
        decimal workedHours,
        decimal overtimeHours,
        int shiftCount,
        string? note)
    {
        if (employeeId == Guid.Empty)
        {
            throw new DomainException("Employee is required.");
        }

        if (workedHours is < 0 or > 24)
        {
            throw new DomainException("Worked hours must be between 0 and 24.");
        }

        if (overtimeHours is < 0 or > 12)
        {
            throw new DomainException("Overtime hours must be between 0 and 12.");
        }

        if (overtimeHours > workedHours)
        {
            throw new DomainException("Overtime hours cannot exceed worked hours.");
        }

        if (shiftCount is < 0 or > 3)
        {
            throw new DomainException("Shift count must be between 0 and 3.");
        }

        EmployeeId = employeeId;
        WorkDate = workDate;
        WorkedHours = workedHours;
        OvertimeHours = overtimeHours;
        ShiftCount = shiftCount;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }

    public Guid EmployeeId { get; private set; }

    public DateOnly WorkDate { get; private set; }

    /// <summary>مجموع ساعت کارکرد در این روز (شامل اضافه‌کار).</summary>
    public decimal WorkedHours { get; private set; }

    /// <summary>بخشی از کارکرد که اضافه‌کار محسوب می‌شود.</summary>
    public decimal OvertimeHours { get; private set; }

    /// <summary>تعداد شیفت ثبت‌شده این روز (برای شیفتی‌ها).</summary>
    public int ShiftCount { get; private set; }

    public string? Note { get; private set; }

    public void Update(
        DateOnly workDate,
        decimal workedHours,
        decimal overtimeHours,
        int shiftCount,
        string? note)
    {
        if (workedHours is < 0 or > 24)
        {
            throw new DomainException("Worked hours must be between 0 and 24.");
        }

        if (overtimeHours is < 0 or > 12)
        {
            throw new DomainException("Overtime hours must be between 0 and 12.");
        }

        if (overtimeHours > workedHours)
        {
            throw new DomainException("Overtime hours cannot exceed worked hours.");
        }

        if (shiftCount is < 0 or > 3)
        {
            throw new DomainException("Shift count must be between 0 and 3.");
        }

        WorkDate = workDate;
        WorkedHours = workedHours;
        OvertimeHours = overtimeHours;
        ShiftCount = shiftCount;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }
}
