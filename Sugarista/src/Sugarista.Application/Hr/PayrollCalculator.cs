using Sugarista.Domain.Entities;

namespace Sugarista.Application.Hr;

/// <summary>
/// Pure payroll math shared by period creation/refresh and the UI-preview:
/// one place to keep the «how is pay computed» rules consistent.
/// </summary>
public static class PayrollCalculator
{
    /// <summary>Standard monthly working hours used to derive an hourly rate from a fixed salary.</summary>
    public const decimal StandardMonthlyHours = 176m;

    /// <summary>An 8-hour shift is the base for deriving hourly rates of shift workers.</summary>
    public const decimal HoursPerShift = 8m;

    public static (decimal EffectiveHourlyRate, decimal BasePay) Compute(
        EmployeePayPlan plan,
        decimal workedHours,
        decimal overtimeHours,
        int shiftCount)
    {
        return plan.Kind switch
        {
            PayPlanKind.Monthly => (
                plan.SalaryAmount / StandardMonthlyHours,
                plan.SalaryAmount),

            PayPlanKind.MonthlyPlusCommission => (
                plan.SalaryAmount / StandardMonthlyHours,
                plan.SalaryAmount),

            PayPlanKind.Hourly => (
                plan.HourlyRate,
                plan.HourlyRate * (workedHours - overtimeHours)),

            PayPlanKind.HourlyPlusCommission => (
                plan.HourlyRate,
                plan.HourlyRate * (workedHours - overtimeHours)),

            PayPlanKind.ShiftBased => (
                plan.ShiftRate / HoursPerShift,
                plan.ShiftRate * shiftCount),

            PayPlanKind.CommissionOnly => (0m, 0m),

            _ => (0m, 0m),
        };
    }

    public static decimal OvertimePay(decimal overtimeHours, decimal effectiveHourlyRate, decimal multiplier)
    {
        return decimal.Round(overtimeHours * effectiveHourlyRate * multiplier, 0, MidpointRounding.AwayFromZero);
    }
}
