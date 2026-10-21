using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Extensions;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Hr.Employees.GetEmployees;

public record GetEmployeesQuery(
    int Page = 1,
    int PageSize = 25,
    string? Search = null,
    bool? ActiveOnly = null)
    : IRequest<PaginatedList<Sugarista.Application.Hr.Employees.CreateEmployee.EmployeeDto>>;

public class GetEmployeesQueryHandler : IRequestHandler<GetEmployeesQuery, PaginatedList<Sugarista.Application.Hr.Employees.CreateEmployee.EmployeeDto>>
{
    private readonly IApplicationDbContext _dbContext;

    public GetEmployeesQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PaginatedList<Sugarista.Application.Hr.Employees.CreateEmployee.EmployeeDto>> Handle(
        GetEmployeesQuery request,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.Employees
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(employee =>
                EF.Functions.Like(employee.PersianName, $"%{term}%")
                || (employee.EnglishName != null && EF.Functions.Like(employee.EnglishName, $"%{term}%"))
                || (employee.PhoneNumber != null && EF.Functions.Like(employee.PhoneNumber, $"%{term}%"))
                || EF.Functions.Like(employee.Position, $"%{term}%"));
        }

        if (request.ActiveOnly == true)
        {
            query = query.Where(employee => employee.IsActive);
        }

        return await query
            .OrderByDescending(employee => employee.IsActive)
            .ThenBy(employee => employee.PersianName)
            .Select(employee => new Sugarista.Application.Hr.Employees.CreateEmployee.EmployeeDto(
                employee.Id,
                employee.PersianName,
                employee.EnglishName,
                employee.PhoneNumber,
                employee.NationalId,
                employee.Position,
                employee.HireDate,
                employee.TerminationDate,
                employee.IsActive,
                employee.Notes,
                _dbContext.EmployeePayPlans
                    .Where(plan => plan.IsActive && plan.EmployeeId == employee.Id)
                    .OrderByDescending(plan => plan.CreatedAt)
                    .Select(plan => new Sugarista.Application.Hr.Employees.CreateEmployee.EmployeePayPlanDto(
                        plan.Id,
                        plan.Kind.ToString(),
                        plan.SalaryAmount,
                        plan.HourlyRate,
                        plan.ShiftRate,
                        plan.CommissionPercent,
                        plan.OvertimeMultiplier,
                        plan.Notes,
                        plan.IsActive,
                        plan.CreatedAt))
                    .FirstOrDefault(),
                employee.WorkLogs.Count,
                employee.CreatedAt))
            .ToPaginatedListAsync(
                new PaginationQuery(request.Page, request.PageSize, request.Search),
                cancellationToken);
    }
}
