using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

/// <summary>
/// One version of a staff member's pay scheme. Creating a new plan
/// deactivates the previous one — history is kept for audits and receipts.
/// </summary>
public class EmployeePayPlan : BaseAuditableEntity
{
    private EmployeePayPlan()
    {
    }

    public EmployeePayPlan(
        Guid employeeId,
        PayPlanKind kind,
        decimal salaryAmount,
        decimal hourlyRate,
        decimal shiftRate,
        decimal commissionPercent,
        decimal overtimeMultiplier,
        string? notes)
    {
        if (employeeId == Guid.Empty)
        {
            throw new DomainException("Employee is required.");
        }

        if (salaryAmount < 0)
        {
            throw new DomainException("Salary amount cannot be negative.");
        }

        if (hourlyRate < 0)
        {
            throw new DomainException("Hourly rate cannot be negative.");
        }

        if (shiftRate < 0)
        {
            throw new DomainException("Shift rate cannot be negative.");
        }

        if (commissionPercent is < 0 or > 100)
        {
            throw new DomainException("Commission percent must be between 0 and 100.");
        }

        if (overtimeMultiplier <= 0)
        {
            throw new DomainException("Overtime multiplier must be a positive number.");
        }

        EmployeeId = employeeId;
        Kind = kind;
        SalaryAmount = salaryAmount;
        HourlyRate = hourlyRate;
        ShiftRate = shiftRate;
        CommissionPercent = commissionPercent;
        OvertimeMultiplier = overtimeMultiplier;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        IsActive = true;
    }

    public Guid EmployeeId { get; private set; }

    public PayPlanKind Kind { get; private set; }

    /// <summary>حقوق ثابت ماهانه — برای Monthly / MonthlyPlusCommission.</summary>
    public decimal SalaryAmount { get; private set; }

    /// <summary>نرخ هر ساعت — برای Hourly / HourlyPlusCommission.</summary>
    public decimal HourlyRate { get; private set; }

    /// <summary>نرخ هر شیفت — برای ShiftBased.</summary>
    public decimal ShiftRate { get; private set; }

    /// <summary>درصد پورسانت از فروش منسوب — برای انواع Percent-based.</summary>
    public decimal CommissionPercent { get; private set; }

    /// <summary>ضریب ساعت اضافه‌کار نسبت به نرخ پایه (مثل ۱.۴).</summary>
    public decimal OvertimeMultiplier { get; private set; }

    public string? Notes { get; private set; }

    /// <summary>False = نسخه قبلی پرداخت (تاریخچه).</summary>
    public bool IsActive { get; private set; }

    /// <summary>Computes the base pay for a period from the recorded work.</summary>
    public decimal ComputeBasePay(decimal workedHours, decimal totalShifts)
    {
        if (Kind is PayPlanKind.Monthly or PayPlanKind.MonthlyPlusCommission)
        {
            return SalaryAmount;
        }

        if (Kind is PayPlanKind.Hourly or PayPlanKind.HourlyPlusCommission)
        {
            return HourlyRate * (workedHours - ComputeOvertimeHours(workedHours, 0m));
        }

        if (Kind is PayPlanKind.CommissionOnly)
        {
            return 0m;
        }

        return ShiftRate * totalShifts;
    }

    public decimal ComputeOvertimeHours(decimal workedHours, decimal recordOvertimeHours)
    {
        return recordOvertimeHours > 0 ? recordOvertimeHours : 0m;
    }
}
