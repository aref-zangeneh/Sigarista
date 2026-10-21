using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Hr.Periods.CreatePayrollPeriod;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Hr.Periods.GetPayrollPeriodDetail;

public record GetPayrollPeriodDetailQuery(Guid Id) : IRequest<PayrollPeriodDetailDto>;

public class GetPayrollPeriodDetailQueryHandler : IRequestHandler<GetPayrollPeriodDetailQuery, PayrollPeriodDetailDto>
{
    private readonly IApplicationDbContext _dbContext;

    public GetPayrollPeriodDetailQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PayrollPeriodDetailDto> Handle(
        GetPayrollPeriodDetailQuery request,
        CancellationToken cancellationToken)
    {
        var period = await _dbContext.PayrollPeriods
            .AsNoTracking()
            .Include(existing => existing.Entries)
            .FirstOrDefaultAsync(existing => existing.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Payroll period was not found.");

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
