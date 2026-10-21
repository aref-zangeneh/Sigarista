using Microsoft.EntityFrameworkCore;
using Sugarista.Domain.Entities;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Application.Hr.Employees.UpdateEmployee;

public record UpdateEmployeeCommand(
    Guid Id,
    string PersianName,
    string? EnglishName,
    string? PhoneNumber,
    string? NationalId,
    string Position,
    DateOnly HireDate,
    string? Notes) : IRequest<Sugarista.Application.Hr.Employees.CreateEmployee.EmployeeDto>;

public class UpdateEmployeeCommandHandler : IRequestHandler<UpdateEmployeeCommand, Sugarista.Application.Hr.Employees.CreateEmployee.EmployeeDto>
{
    private readonly IApplicationDbContext _dbContext;

    public UpdateEmployeeCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Sugarista.Application.Hr.Employees.CreateEmployee.EmployeeDto> Handle(
        UpdateEmployeeCommand request,
        CancellationToken cancellationToken)
    {
        var employee = await _dbContext.Employees
            .Include(existing => existing.PayPlans)
            .FirstOrDefaultAsync(existing => existing.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Employee was not found.");

        var nameDuplication = await _dbContext.Employees
            .AnyAsync(
                existing => existing.Id != request.Id
                    && existing.PersianName == request.PersianName.Trim()
                    && existing.IsActive,
                cancellationToken);

        if (nameDuplication)
        {
            throw new DomainException("یک پرسنل فعال با همین نام وجود دارد.");
        }

        employee.Update(
            request.PersianName,
            request.EnglishName,
            request.PhoneNumber,
            request.NationalId,
            request.Position,
            request.HireDate,
            request.Notes);

        await _dbContext.SaveChangesAsync(cancellationToken);

        var activePlan = employee.PayPlans
            .Where(plan => plan.IsActive)
            .OrderByDescending(plan => plan.CreatedAt)
            .FirstOrDefault();

        return Sugarista.Application.Hr.Employees.CreateEmployee.EmployeeMapper.ToDto(employee, activePlan);
    }
}
