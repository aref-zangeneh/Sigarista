using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Accounting.Services;
using Sugarista.Domain.Entities;
using Sugarista.Application.Hr.Periods.CreatePayrollPeriod;

namespace Sugarista.Application.Hr.Periods.PayPayrollPeriod;

public record PayPayrollPeriodCommand(Guid PeriodId) : IRequest<PayrollPeriodDetailDto>;

public class PayPayrollPeriodCommandHandler : IRequestHandler<PayPayrollPeriodCommand, PayrollPeriodDetailDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IAccountingService _accountingService;
    private readonly IDateTime _dateTime;

    public PayPayrollPeriodCommandHandler(
        IApplicationDbContext dbContext,
        IAccountingService accountingService,
        IDateTime dateTime)
    {
        _dbContext = dbContext;
        _accountingService = accountingService;
        _dateTime = dateTime;
    }

    public async Task<PayrollPeriodDetailDto> Handle(
        PayPayrollPeriodCommand request,
        CancellationToken cancellationToken)
    {
        var period = await _dbContext.PayrollPeriods
            .Include(existing => existing.Entries)
            .FirstOrDefaultAsync(existing => existing.Id == request.PeriodId, cancellationToken)
            ?? throw new KeyNotFoundException("Payroll period was not found.");

        period.EnsureFinalizedForPayment();

        foreach (var entry in period.Entries
                     .Where(existing => existing.Status == PayrollEntryStatus.Draft && existing.TotalPay > 0)
                     .OrderBy(existing => existing.EmployeeName))
        {
            var journalEntryId = await _accountingService.PostManualExpenseAsync(
                period.EndDate,
                Math.Max(entry.TotalPay, 0),
                $"حقوق {entry.EmployeeName} — دوره {period.ShamsiMonth:00}/{period.ShamsiYear:0000}",
                paidFromCash: true,
                cancellationToken);

            entry.MarkPaid(_dateTime.UtcNow, journalEntryId);
        }

        period.MarkFullyPaidIfSettled();
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
