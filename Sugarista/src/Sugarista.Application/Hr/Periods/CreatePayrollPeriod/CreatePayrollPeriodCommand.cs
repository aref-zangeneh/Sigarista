using Microsoft.EntityFrameworkCore;
using Sugarista.Domain.Entities;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Application.Hr.Periods.CreatePayrollPeriod;

public record CreatePayrollPeriodCommand(int ShamsiYear, int ShamsiMonth) : IRequest<PayrollPeriodDetailDto>;

public record PayrollPeriodDetailDto(
    Guid Id,
    int ShamsiYear,
    int ShamsiMonth,
    string MonthLabel,
    DateOnly StartDate,
    DateOnly EndDate,
    string Status,
    IReadOnlyList<PayrollEntryDto> Entries,
    PayrollTotalsDto Totals);

public record PayrollEntryDto(
    Guid Id,
    Guid EmployeeId,
    string EmployeeName,
    string EmployeePosition,
    string PayPlanKind,
    decimal WorkedHours,
    decimal OvertimeHours,
    int ShiftCount,
    decimal ScheduledHours,
    decimal UnderworkHours,
    decimal UnderworkDeduction,
    decimal BasePay,
    decimal OvertimePay,
    decimal CommissionSalesAmount,
    decimal CommissionPercent,
    decimal CommissionPay,
    decimal Bonus,
    decimal Deductions,
    decimal Advance,
    decimal TotalPay,
    string Status,
    DateTimeOffset? PaidAt,
    string? Note);

public record PayrollTotalsDto(
    decimal TotalPayable,
    decimal TotalPaid,
    decimal TotalRemaining,
    int EntryCount,
    int PaidCount);

public static class PayrollMapper
{
    public static PayrollEntryDto ToDto(PayrollEntry entry) => new(
        entry.Id,
        entry.EmployeeId,
        entry.EmployeeName,
        entry.EmployeePosition,
        entry.PayPlanKind.ToString(),
        entry.WorkedHours,
        entry.OvertimeHours,
        entry.ShiftCount,
        entry.ScheduledHours,
        entry.UnderworkHours,
        entry.UnderworkDeduction,
        entry.BasePay,
        entry.OvertimePay,
        entry.CommissionSalesAmount,
        entry.CommissionPercent,
        entry.CommissionPay,
        entry.Bonus,
        entry.Deductions,
        entry.Advance,
        entry.TotalPay,
        entry.Status.ToString(),
        entry.PaidAt,
        entry.Note);

    public static PayrollTotalsDto ToTotals(IReadOnlyCollection<PayrollEntry> entries)
    {
        var unpaid = entries
            .Where(entry => entry.Status == PayrollEntryStatus.Draft && entry.TotalPay > 0)
            .Sum(entry => entry.TotalPay);
        var paid = entries
            .Where(entry => entry.Status == PayrollEntryStatus.Paid)
            .Sum(entry => Math.Max(entry.TotalPay, 0));

        return new PayrollTotalsDto(
            unpaid,
            paid,
            entries.Where(entry => entry.Status == PayrollEntryStatus.Draft).Sum(entry => Math.Max(entry.TotalPay, 0)),
            entries.Count,
            entries.Count(entry => entry.Status == PayrollEntryStatus.Paid));
    }
}

public class CreatePayrollPeriodCommandHandler : IRequestHandler<CreatePayrollPeriodCommand, PayrollPeriodDetailDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IDateTime _dateTime;

    public CreatePayrollPeriodCommandHandler(IApplicationDbContext dbContext, IDateTime dateTime)
    {
        _dbContext = dbContext;
        _dateTime = dateTime;
    }

    public async Task<PayrollPeriodDetailDto> Handle(
        CreatePayrollPeriodCommand request,
        CancellationToken cancellationToken)
    {
        var (startDate, endDate) = HrShamsiCalendar.MonthRange(request.ShamsiYear, request.ShamsiMonth);

        var period = await _dbContext.PayrollPeriods
            .Include(existing => existing.Entries)
            .FirstOrDefaultAsync(
                existing => existing.ShamsiYear == request.ShamsiYear && existing.ShamsiMonth == request.ShamsiMonth,
                cancellationToken);

        if (period is null)
        {
            period = new PayrollPeriod(request.ShamsiYear, request.ShamsiMonth, startDate, endDate);
            _dbContext.PayrollPeriods.Add(period);
        }
        else
        {
            period.EnsureEditable();
        }

        await BuildOrRefreshEntriesAsync(period, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new PayrollPeriodDetailDto(
            period.Id,
            period.ShamsiYear,
            period.ShamsiMonth,
            HrShamsiCalendar.MonthLabel(period.ShamsiYear, period.ShamsiMonth),
            period.StartDate,
            period.EndDate,
            period.Status.ToString(),
            period.Entries
                .OrderBy(entry => entry.EmployeeName)
                .Select(PayrollMapper.ToDto)
                .ToList(),
            PayrollMapper.ToTotals(period.Entries.ToList()));
    }

    private async Task BuildOrRefreshEntriesAsync(
        PayrollPeriod period,
        CancellationToken cancellationToken)
    {
        var workLogs = await _dbContext.EmployeeWorkLogs
            .AsNoTracking()
            .Where(log => log.WorkDate >= period.StartDate && log.WorkDate <= period.EndDate)
            .ToListAsync(cancellationToken);

        var logsByEmployee = workLogs
            .GroupBy(log => log.EmployeeId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var loggedEmployeeIds = logsByEmployee.Keys.ToList();

        // real presence from the punch buttons — takes precedence over manual logs
        var punches = await _dbContext.PunchAttendances
            .AsNoTracking()
            .Where(attendance => attendance.WorkDate >= period.StartDate
                && attendance.WorkDate <= period.EndDate
                && attendance.PunchInAt != null
                && attendance.PunchOutAt != null)
            .ToListAsync(cancellationToken);

        var workedMinutesByEmployee = punches
            .GroupBy(attendance => attendance.EmployeeId)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(attendance => attendance.WorkedMinutes));

        var punchEmployeeIds = workedMinutesByEmployee.Keys.ToList();

        // employees active now, or inactive-but-worked this period
        var employees = await _dbContext.Employees
            .AsNoTracking()
            .Include(employee => employee.PayPlans)
            .Include(employee => employee.ShiftPlans)
            .Where(employee => employee.IsActive
                || loggedEmployeeIds.Contains(employee.Id)
                || punchEmployeeIds.Contains(employee.Id))
            .ToListAsync(cancellationToken);

        foreach (var employee in employees)
        {
            var plan = employee.PayPlans
                .Where(existing => existing.IsActive)
                .OrderByDescending(existing => existing.CreatedAt)
                .FirstOrDefault()
                ?? employee.PayPlans
                    .OrderByDescending(existing => existing.CreatedAt)
                    .FirstOrDefault();

            if (plan is null)
            {
                continue; // no pay structure yet — nothing to compute
            }
            
            var shiftPlans = employee.ShiftPlans.ToList();

            // approved scheduled hours of the period from the weekly shift plan
            var scheduledHours = ComputeScheduledHours(period, shiftPlans);

            decimal worked;
            decimal overtime;
            var shifts = 0;

            if (workedMinutesByEmployee.TryGetValue(employee.Id, out var punchMinutes))
            {
                // punches drive the numbers; overtime = real presence over approved shifts
                worked = decimal.Round(punchMinutes / 60m, 2, MidpointRounding.AwayFromZero);
                overtime = scheduledHours > 0 && worked > scheduledHours
                    ? decimal.Round(worked - scheduledHours, 2)
                    : 0m;
            }
            else
            {
                // manual work-log fallback (no punch data this period)
                var logs = logsByEmployee.GetValueOrDefault(employee.Id) ?? [];
                worked = logs.Sum(log => log.WorkedHours);
                overtime = logs.Sum(log => log.OvertimeHours);
                shifts = logs.Sum(log => log.ShiftCount);
            }

            var underwork = scheduledHours > 0 && worked < scheduledHours
                ? decimal.Round(scheduledHours - worked, 2)
                : 0m;

            var (effectiveHourly, basePay) = PayrollCalculator.Compute(plan, worked, overtime, shifts);
            var overtimePay = PayrollCalculator.OvertimePay(overtime, effectiveHourly, plan.OvertimeMultiplier);
            var underworkDeduction = PayrollCalculator.UnderworkDeduction(
                plan.Kind,
                underwork,
                effectiveHourly);

            var entry = period.Entries.FirstOrDefault(existing => existing.EmployeeId == employee.Id);
            if (entry is null)
            {
                entry = new PayrollEntry(
                    period.Id,
                    employee.Id,
                    employee.PersianName,
                    employee.Position,
                    plan.Kind,
                    worked,
                    overtime,
                    shifts,
                    basePay,
                    overtimePay,
                    0m,
                    plan.CommissionPercent);
                period.Entries.Add(entry);
                _dbContext.PayrollEntries.Add(entry);
                entry.SetSchedule(scheduledHours, underwork, underworkDeduction);
            }
            else
            {
                entry.RefreshWorkData(
                    worked,
                    overtime,
                    shifts,
                    basePay,
                    effectiveHourly * plan.OvertimeMultiplier,
                    plan.CommissionPercent,
                    scheduledHours,
                    underwork,
                    underworkDeduction);
            }
        }
    }

    /// <summary>Sum of approved shift lengths across every day of the period.</summary>
    private static decimal ComputeScheduledHours(PayrollPeriod period, List<EmployeeShiftPlan> shiftPlans)
    {
        if (shiftPlans.Count == 0)
        {
            return 0m;
        }

        var plannedMinutes = 0m;
        for (var day = period.StartDate; day <= period.EndDate; day = day.AddDays(1))
        {
            var weekday = (int)day.DayOfWeek;
            var plan = shiftPlans.FirstOrDefault(item => item.Weekday == weekday);
            if (plan is not null)
            {
                plannedMinutes += plan.ScheduledMinutes;
            }
        }

        return decimal.Round(plannedMinutes / 60m, 2, MidpointRounding.AwayFromZero);
    }
}
