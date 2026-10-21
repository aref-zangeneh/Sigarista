using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

/// <summary>
/// A cafe staff member's HR file (پرونده پرسنلی): identity, position, hire date
/// and pay-plan history. Employees are never hard-deleted — deactivated instead.
/// </summary>
public class Employee : BaseAuditableEntity
{
    private Employee()
    {
    }

    public Employee(
        string persianName,
        string? englishName,
        string? phoneNumber,
        string? nationalId,
        string position,
        DateOnly hireDate,
        string? notes)
    {
        SetName(persianName);
        SetPosition(position);
        EnglishName = string.IsNullOrWhiteSpace(englishName) ? null : englishName.Trim();
        PhoneNumber = string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber.Trim();
        NationalId = string.IsNullOrWhiteSpace(nationalId) ? null : nationalId.Trim();
        HireDate = hireDate;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        IsActive = true;
        PayPlans = [];
        WorkLogs = [];
        PayrollEntries = [];
    }

    public string PersianName { get; private set; } = string.Empty;

    public string? EnglishName { get; private set; }

    public string? PhoneNumber { get; private set; }

    public string? NationalId { get; private set; }

    /// <summary>مثلاً باریستا، کاسیِر، سرآشپز، نظافتچی.</summary>
    public string Position { get; private set; } = string.Empty;

    public DateOnly HireDate { get; private set; }

    public DateOnly? TerminationDate { get; private set; }

    public bool IsActive { get; private set; }

    public string? Notes { get; private set; }

    public ICollection<EmployeePayPlan> PayPlans { get; private set; } = [];

    public ICollection<EmployeeWorkLog> WorkLogs { get; private set; } = [];

    public ICollection<PayrollEntry> PayrollEntries { get; private set; } = [];

    public void Update(
        string persianName,
        string? englishName,
        string? phoneNumber,
        string? nationalId,
        string position,
        DateOnly hireDate,
        string? notes)
    {
        SetName(persianName);
        SetPosition(position);
        EnglishName = string.IsNullOrWhiteSpace(englishName) ? null : englishName.Trim();
        PhoneNumber = string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber.Trim();
        NationalId = string.IsNullOrWhiteSpace(nationalId) ? null : nationalId.Trim();
        HireDate = hireDate;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
    }

    /// <summary>Deactivates the file (staff left). Kept for payroll history.</summary>
    public void Deactivate(DateOnly? terminationDate)
    {
        TerminationDate = terminationDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        IsActive = false;
    }

    public void Activate()
    {
        TerminationDate = null;
        IsActive = true;
    }

    private void SetName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException("Persian name is required.");
        }

        PersianName = value.Trim();
    }

    private void SetPosition(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException("Position is required.");
        }

        Position = value.Trim();
    }
}
