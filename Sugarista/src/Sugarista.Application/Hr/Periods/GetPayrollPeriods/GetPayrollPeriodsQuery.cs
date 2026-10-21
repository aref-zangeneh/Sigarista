using Microsoft.EntityFrameworkCore;
using Sugarista.Domain.Entities;
using Sugarista.Application.Hr.Periods.CreatePayrollPeriod;

namespace Sugarista.Application.Hr.Periods.GetPayrollPeriods;

public record PayrollPeriodSummaryDto(
    Guid Id,
    int ShamsiYear,
    int ShamsiMonth,
    string MonthLabel,
    string Status,
    int EntryCount,
    int PaidCount,
    decimal TotalPayable,
    decimal TotalPaid,
    DateTimeOffset CreatedAt);

public record GetPayrollPeriodsQuery : IRequest<IReadOnlyList<PayrollPeriodSummaryDto>>;

public class GetPayrollPeriodsQueryHandler : IRequestHandler<GetPayrollPeriodsQuery, IReadOnlyList<PayrollPeriodSummaryDto>>
{
    private readonly IApplicationDbContext _dbContext;

    public GetPayrollPeriodsQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<PayrollPeriodSummaryDto>> Handle(
        GetPayrollPeriodsQuery request,
        CancellationToken cancellationToken)
    {
        var periods = await _dbContext.PayrollPeriods
            .AsNoTracking()
            .Include(period => period.Entries)
            .OrderByDescending(period => period.ShamsiYear)
            .ThenByDescending(period => period.ShamsiMonth)
            .ToListAsync(cancellationToken);

        return periods.Select(period => new PayrollPeriodSummaryDto(
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
            period.CreatedAt)).ToList();
    }
}
