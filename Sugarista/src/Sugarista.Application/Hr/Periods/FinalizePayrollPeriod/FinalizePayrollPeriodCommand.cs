using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Hr.Periods.CreatePayrollPeriod;

namespace Sugarista.Application.Hr.Periods.FinalizePayrollPeriod;

public record FinalizePayrollPeriodCommand(Guid PeriodId) : IRequest<PayrollPeriodDetailDto>;

public class FinalizePayrollPeriodCommandHandler : IRequestHandler<FinalizePayrollPeriodCommand, PayrollPeriodDetailDto>
{
    private readonly IApplicationDbContext _dbContext;

    public FinalizePayrollPeriodCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PayrollPeriodDetailDto> Handle(
        FinalizePayrollPeriodCommand request,
        CancellationToken cancellationToken)
    {
        var period = await _dbContext.PayrollPeriods
            .Include(existing => existing.Entries)
            .FirstOrDefaultAsync(existing => existing.Id == request.PeriodId, cancellationToken)
            ?? throw new KeyNotFoundException("Payroll period was not found.");

        period.FinalizePeriod();
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
}
