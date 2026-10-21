using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Accounting.Services;
using Sugarista.Application.Hr.Periods.CreatePayrollPeriod;
using Sugarista.Domain.Entities;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Application.Hr.Periods.PayPayrollEntry;

public record PayPayrollEntryCommand(Guid PeriodId, Guid EntryId) : IRequest<PayrollPeriodDetailDto>;

public class PayPayrollEntryCommandHandler : IRequestHandler<PayPayrollEntryCommand, PayrollPeriodDetailDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IAccountingService _accountingService;
    private readonly IDateTime _dateTime;

    public PayPayrollEntryCommandHandler(
        IApplicationDbContext dbContext,
        IAccountingService accountingService,
        IDateTime dateTime)
    {
        _dbContext = dbContext;
        _accountingService = accountingService;
        _dateTime = dateTime;
    }

    public async Task<PayrollPeriodDetailDto> Handle(
        PayPayrollEntryCommand request,
        CancellationToken cancellationToken)
    {
        var period = await _dbContext.PayrollPeriods
            .Include(existing => existing.Entries)
            .FirstOrDefaultAsync(existing => existing.Id == request.PeriodId, cancellationToken)
            ?? throw new KeyNotFoundException("Payroll period was not found.");

        var entry = period.Entries.FirstOrDefault(existing => existing.Id == request.EntryId)
            ?? throw new KeyNotFoundException("Payroll entry was not found.");

        period.EnsureFinalizedForPayment();

        if (entry.TotalPay <= 0)
        {
            throw new DomainException("این فیش مبلغ قابل پرداختی ندارد (جمع صفر یا منفی است).");
        }

        var journalEntryId = await PayAsync(period, entry, cancellationToken);

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

    private async Task<Guid> PayAsync(
        PayrollPeriod period,
        PayrollEntry entry,
        CancellationToken cancellationToken)
    {
        var entryId = await _accountingService.PostManualExpenseAsync(
            period.EndDate,
            Math.Max(entry.TotalPay, 0),
            $"حقوق {entry.EmployeeName} — دوره {period.ShamsiMonth:00}/{period.ShamsiYear:0000}",
            paidFromCash: true,
            cancellationToken);

        entry.MarkPaid(_dateTime.UtcNow, entryId);
        period.MarkFullyPaidIfSettled();
        return entryId;
    }
}
