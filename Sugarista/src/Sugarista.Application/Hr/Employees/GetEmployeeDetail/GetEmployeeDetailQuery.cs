using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Hr.Employees.CreateEmployee;

namespace Sugarista.Application.Hr.Employees.GetEmployeeDetail;

/// <summary>Full HR file: identity, plan history, recent work logs and payroll entries.</summary>
public record GetEmployeeDetailQuery(Guid Id) : IRequest<EmployeeDetailDto>;

public record EmployeeDetailDto(
    Guid Id,
    string PersianName,
    string? EnglishName,
    string? PhoneNumber,
    string? NationalId,
    string Position,
    DateOnly HireDate,
    DateOnly? TerminationDate,
    bool IsActive,
    string? Notes,
    IReadOnlyList<EmployeePayPlanDto> PayPlanHistory,
    IReadOnlyList<WorkLogDto> RecentWorkLogs,
    IReadOnlyList<PayrollEntryMiniDto> PayrollEntries);

public record WorkLogDto(
    Guid Id,
    Guid EmployeeId,
    DateOnly WorkDate,
    decimal WorkedHours,
    decimal OvertimeHours,
    int ShiftCount,
    string? Note);

public record PayrollEntryMiniDto(
    Guid Id,
    string PeriodLabel,
    string Status,
    decimal TotalPay);

public class GetEmployeeDetailQueryHandler : IRequestHandler<GetEmployeeDetailQuery, EmployeeDetailDto>
{
    private readonly IApplicationDbContext _dbContext;

    public GetEmployeeDetailQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<EmployeeDetailDto> Handle(
        GetEmployeeDetailQuery request,
        CancellationToken cancellationToken)
    {
        var employee = await _dbContext.Employees
            .AsNoTracking()
            .Include(selected => selected.PayPlans)
            .Include(selected => selected.WorkLogs)
            .Include(selected => selected.PayrollEntries)!.ThenInclude(entry => entry.Period)
            .FirstOrDefaultAsync(selected => selected.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Employee was not found.");

        var activePlan = employee.PayPlans
            .Where(plan => plan.IsActive)
            .OrderByDescending(plan => plan.CreatedAt)
            .FirstOrDefault();

        var baseDto = EmployeeMapper.ToDto(employee, activePlan);

        return new EmployeeDetailDto(
            baseDto.Id,
            baseDto.PersianName,
            baseDto.EnglishName,
            baseDto.PhoneNumber,
            baseDto.NationalId,
            baseDto.Position,
            baseDto.HireDate,
            baseDto.TerminationDate,
            baseDto.IsActive,
            baseDto.Notes,
            employee.PayPlans
                .OrderByDescending(plan => plan.CreatedAt)
                .Select(Sugarista.Application.Hr.Employees.CreateEmployee.EmployeeMapper.ToDto)
                .ToList(),
            employee.WorkLogs
                .OrderByDescending(log => log.WorkDate)
                .Take(30)
                .Select(log => new WorkLogDto(
                    log.Id,
                    log.EmployeeId,
                    log.WorkDate,
                    log.WorkedHours,
                    log.OvertimeHours,
                    log.ShiftCount,
                    log.Note))
                .ToList(),
            employee.PayrollEntries
                .Where(entry => entry.Period != null)
                .OrderByDescending(entry => entry.Period!.ShamsiYear)
                .ThenByDescending(entry => entry.Period!.ShamsiMonth)
                .Take(12)
                .Select(entry => new PayrollEntryMiniDto(
                    entry.Id,
                    $"{entry.Period!.ShamsiMonth:00}/{entry.Period.ShamsiYear:0000}",
                    entry.Status.ToString(),
                    entry.TotalPay))
                .ToList());
    }
}
