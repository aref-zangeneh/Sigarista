using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Hr.Periods.GetPayrollPeriods;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Hr.Periods.ReopenPayrollPeriod;

public record ReopenPayrollPeriodCommand(Guid PeriodId) : IRequest<PayrollPeriodSummaryDto?>;

public class ReopenPayrollPeriodCommandHandler : IRequestHandler<ReopenPayrollPeriodCommand, PayrollPeriodSummaryDto?>
{
    private readonly IApplicationDbContext _dbContext;

    public ReopenPayrollPeriodCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PayrollPeriodSummaryDto?> Handle(
        ReopenPayrollPeriodCommand request,
        CancellationToken cancellationToken)
    {
        var period = await _dbContext.PayrollPeriods
            .Include(existing => existing.Entries)
            .FirstOrDefaultAsync(existing => existing.Id == request.PeriodId, cancellationToken)
            ?? throw new KeyNotFoundException("Payroll period was not found.");

        period.Reopen();
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new PayrollPeriodSummaryDto(
            period.Id,
            period.ShamsiYear,
            period.ShamsiMonth,
            HrShamsiCalendar.MonthLabel(period.ShamsiYear, period.ShamsiMonth),
            period.Status.ToString(),
            period.Entries.Count,
            period.Entries.Count(entry => entry.Status == PayrollEntryStatus.Paid),
            period.Entries
                .Where(entry => entry.Status == PayrollEntryStatus.Draft)
                .Sum(entry => Math.Max(entry.TotalPay, 0)),
            period.Entries
                .Where(entry => entry.Status == PayrollEntryStatus.Paid)
                .Sum(entry => Math.Max(entry.TotalPay, 0)),
            period.CreatedAt);
    }
}
