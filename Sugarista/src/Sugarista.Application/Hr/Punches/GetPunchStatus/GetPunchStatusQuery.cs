using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Hr.Punches.PunchIn;

namespace Sugarista.Application.Hr.Punches.GetPunchStatus;

/// <summary>Live punch state of every active employee for the café dashboard.</summary>
public record EmployeePunchStatusDto(
    Guid EmployeeId,
    string EmployeeName,
    string Position,
    string State,
    Guid? OpenPunchId,
    DateTimeOffset? PunchInAt);

public record GetPunchStatusQuery : IRequest<IReadOnlyList<EmployeePunchStatusDto>>;

public class GetPunchStatusQueryHandler : IRequestHandler<GetPunchStatusQuery, IReadOnlyList<EmployeePunchStatusDto>>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IDateTime _dateTime;

    public GetPunchStatusQueryHandler(IApplicationDbContext dbContext, IDateTime dateTime)
    {
        _dbContext = dbContext;
        _dateTime = dateTime;
    }

    public async Task<IReadOnlyList<EmployeePunchStatusDto>> Handle(
        GetPunchStatusQuery request,
        CancellationToken cancellationToken)
    {
        var today = CafeClock.CafeToday(_dateTime.UtcNow);

        var employees = await _dbContext.Employees
            .AsNoTracking()
            .Where(employee => employee.IsActive)
            .OrderBy(employee => employee.PersianName)
            .ToListAsync(cancellationToken);

        var todayPunches = await _dbContext.PunchAttendances
            .AsNoTracking()
            .Where(attendance => attendance.WorkDate == today)
            .ToListAsync(cancellationToken);

        var openPunches = await _dbContext.PunchAttendances
            .AsNoTracking()
            .Where(attendance => attendance.PunchInAt != null && attendance.PunchOutAt == null)
            .ToListAsync(cancellationToken);

        var punchesByEmployee = todayPunches
            .GroupBy(attendance => attendance.EmployeeId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var openByEmployee = openPunches
            .GroupBy(attendance => attendance.EmployeeId)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(attendance => attendance.PunchInAt).First());

        var result = new List<EmployeePunchStatusDto>(employees.Count);
        foreach (var employee in employees)
        {
            var todayPunch = punchesByEmployee.GetValueOrDefault(employee.Id)?.FirstOrDefault();
            var punchOutTime = todayPunch?.PunchOutAt;

            var state = openByEmployee.TryGetValue(employee.Id, out var openPunch)
                ? "In"
                : punchOutTime.HasValue ? "Out" : "NotPunched";

            result.Add(new EmployeePunchStatusDto(
                employee.Id,
                employee.PersianName,
                employee.Position,
                state,
                openPunch?.Id,
                openPunch?.PunchInAt ?? todayPunch?.PunchInAt));
        }

        return result;
    }
}
