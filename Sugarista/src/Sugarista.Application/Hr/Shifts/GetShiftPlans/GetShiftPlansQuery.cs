using Microsoft.EntityFrameworkCore;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Hr.Shifts.GetShiftPlans;

public record ShiftPlanDto(
    Guid Id,
    Guid EmployeeId,
    string EmployeeName,
    int Weekday,
    string WeekdayLabel,
    string StartTime,
    string EndTime,
    decimal ScheduledMinutes);

public record GetShiftPlansQuery(
    Guid? EmployeeId = null) : IRequest<IReadOnlyList<ShiftPlanDto>>;

public class GetShiftPlansQueryHandler : IRequestHandler<GetShiftPlansQuery, IReadOnlyList<ShiftPlanDto>>
{
    private readonly IApplicationDbContext _dbContext;

    public GetShiftPlansQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<ShiftPlanDto>> Handle(
        GetShiftPlansQuery request,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.EmployeeShiftPlans
            .AsNoTracking()
            .Include(plan => plan.Employee)
            .AsQueryable();

        if (request.EmployeeId.HasValue)
        {
            query = query.Where(plan => plan.EmployeeId == request.EmployeeId.Value);
        }

        var plans = await query
            .ToListAsync(cancellationToken);

        return plans
            .Where(plan => plan.Employee is not null)
            .OrderBy(plan => plan.Employee!.PersianName)
            .ThenBy(plan => plan.Weekday)
            .Select(plan => new ShiftPlanDto(
                plan.Id,
                plan.EmployeeId,
                plan.Employee!.PersianName,
                plan.Weekday,
                HrShamsiCalendar.WeekdayLabel(plan.Weekday),
                plan.StartTime.ToString("HH:mm"),
                plan.EndTime.ToString("HH:mm"),
                plan.ScheduledMinutes)).ToList();
    }
}
