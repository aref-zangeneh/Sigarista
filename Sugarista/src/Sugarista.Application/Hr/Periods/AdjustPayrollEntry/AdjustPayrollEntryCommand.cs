using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Hr.Periods.CreatePayrollPeriod;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Hr.Periods.AdjustPayrollEntry;

/// <summary>
/// Manual adjustment of a draft payroll receipt: the attributed commission
/// sales, bonus, deductions, advances and an optional note. Extra step to
/// protect the auto-calculated parts (work hours / base pay) which refresh
/// only from work logs.
/// </summary>
public record AdjustPayrollEntryCommand(
    Guid PeriodId,
    Guid EntryId,
    decimal CommissionSalesAmount,
    decimal Bonus,
    decimal Deductions,
    decimal Advance,
    string? Note) : IRequest<PayrollPeriodDetailDto>;

public class AdjustPayrollEntryCommandHandler : IRequestHandler<AdjustPayrollEntryCommand, PayrollPeriodDetailDto>
{
    private readonly IApplicationDbContext _dbContext;

    public AdjustPayrollEntryCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PayrollPeriodDetailDto> Handle(
        AdjustPayrollEntryCommand request,
        CancellationToken cancellationToken)
    {
        var period = await _dbContext.PayrollPeriods
            .Include(existing => existing.Entries)
            .FirstOrDefaultAsync(existing => existing.Id == request.PeriodId, cancellationToken)
            ?? throw new KeyNotFoundException("Payroll period was not found.");

        period.EnsureEditable();

        var entry = period.Entries.FirstOrDefault(existing => existing.Id == request.EntryId)
            ?? throw new KeyNotFoundException("Payroll entry was not found.");

        entry.SetPaidInfo(request.CommissionSalesAmount, request.Note);
        entry.AdjustManualParts(request.Bonus, request.Deductions, request.Advance, request.Note);

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
                .OrderBy(existing => existing.EmployeeName)
                .Select(PayrollMapper.ToDto)
                .ToList(),
            PayrollMapper.ToTotals(period.Entries.ToList()));
    }
}
