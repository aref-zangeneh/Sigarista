using FluentValidation;
using Sugarista.Domain.Entities;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Application.Hr.Employees.CreateEmployee;

public record CreateEmployeeCommand(
    string PersianName,
    string? EnglishName,
    string? PhoneNumber,
    string? NationalId,
    string Position,
    DateOnly HireDate,
    string? Notes) : IRequest<EmployeeDto>;

public record EmployeeDto(
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
    EmployeePayPlanDto? ActivePayPlan,
    int WorkLogsCount,
    DateTimeOffset CreatedAt);

public record EmployeePayPlanDto(
    Guid Id,
    string Kind,
    decimal SalaryAmount,
    decimal HourlyRate,
    decimal ShiftRate,
    decimal CommissionPercent,
    decimal OvertimeMultiplier,
    string? Notes,
    bool IsActive,
    DateTimeOffset CreatedAt);

public static class EmployeeMapper
{
    public static EmployeePayPlanDto ToDto(EmployeePayPlan plan) => new(
        plan.Id,
        plan.Kind.ToString(),
        plan.SalaryAmount,
        plan.HourlyRate,
        plan.ShiftRate,
        plan.CommissionPercent,
        plan.OvertimeMultiplier,
        plan.Notes,
        plan.IsActive,
        plan.CreatedAt);

    public static EmployeeDto ToDto(Employee employee, EmployeePayPlan? activePlan)
        => new(
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
            activePlan is null ? null : ToDto(activePlan),
            employee.WorkLogs?.Count ?? 0,
            employee.CreatedAt);
}

public class CreateEmployeeCommandValidator : AbstractValidator<CreateEmployeeCommand>
{
    public CreateEmployeeCommandValidator()
    {
        RuleFor(command => command.PersianName)
            .NotEmpty()
            .MaximumLength(120);

        RuleFor(command => command.Position)
            .NotEmpty()
            .MaximumLength(120);

        RuleFor(command => command.EnglishName)
            .MaximumLength(120);

        RuleFor(command => command.PhoneNumber)
            .MaximumLength(30);

        RuleFor(command => command.NationalId)
            .MaximumLength(30);
    }
}

public class CreateEmployeeCommandHandler : IRequestHandler<CreateEmployeeCommand, EmployeeDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IDateTime _dateTime;

    public CreateEmployeeCommandHandler(IApplicationDbContext dbContext, IDateTime dateTime)
    {
        _dbContext = dbContext;
        _dateTime = dateTime;
    }

    public async Task<EmployeeDto> Handle(
        CreateEmployeeCommand request,
        CancellationToken cancellationToken)
    {
        var nameDuplication = await _dbContext.Employees
            .AnyAsync(
                employee => employee.PersianName == request.PersianName.Trim() && employee.IsActive,
                cancellationToken);

        if (nameDuplication)
        {
            throw new DomainException("یک پرسنل فعال با همین نام وجود دارد.");
        }

        var employee = new Employee(
            request.PersianName,
            request.EnglishName,
            request.PhoneNumber,
            request.NationalId,
            request.Position,
            request.HireDate,
            request.Notes);

        _dbContext.Employees.Add(employee);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return EmployeeMapper.ToDto(employee, null);
    }
}
