using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Hr.Employees.CreateEmployee;
using Sugarista.Domain.Entities;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Application.Hr.Employees.UpsertPayPlan;

/// <summary>
/// Registers (or replaces) the pay scheme. The previous active plan is
/// deactivated, not deleted — the history stays for audits and references.
/// </summary>
public record UpsertEmployeePayPlanCommand(
    Guid EmployeeId,
    string Kind,
    decimal SalaryAmount,
    decimal HourlyRate,
    decimal ShiftRate,
    decimal CommissionPercent,
    decimal OvertimeMultiplier,
    string? Notes) : IRequest<EmployeeDto>;

public class UpsertEmployeePayPlanCommandValidator : AbstractValidator<UpsertEmployeePayPlanCommand>
{
    public UpsertEmployeePayPlanCommandValidator()
    {
        RuleFor(command => command.Kind)
            .NotEmpty()
            .Must(kind => PayPlanKindParser.TryParse(kind, out _))
            .WithMessage("نوع پرداخت نامعتبر است.");

        RuleFor(command => command.SalaryAmount)
            .GreaterThanOrEqualTo(0);

        RuleFor(command => command.HourlyRate)
            .GreaterThanOrEqualTo(0);

        RuleFor(command => command.ShiftRate)
            .GreaterThanOrEqualTo(0);

        RuleFor(command => command.CommissionPercent)
            .InclusiveBetween(0, 100);

        RuleFor(command => command.OvertimeMultiplier)
            .GreaterThan(0);
    }
}

public static class PayPlanKindParser
{
    public static bool TryParse(string? value, out Sugarista.Domain.Entities.PayPlanKind kind)
    {
        kind = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return Enum.TryParse(value.Trim(), ignoreCase: true, out kind)
            && Enum.IsDefined(typeof(Sugarista.Domain.Entities.PayPlanKind), kind);
    }
}

public class UpsertEmployeePayPlanCommandHandler : IRequestHandler<UpsertEmployeePayPlanCommand, EmployeeDto>
{
    private readonly IApplicationDbContext _dbContext;

    public UpsertEmployeePayPlanCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<EmployeeDto> Handle(
        UpsertEmployeePayPlanCommand request,
        CancellationToken cancellationToken)
    {
        if (!PayPlanKindParser.TryParse(request.Kind, out var kind))
        {
            throw new DomainException("نوع پرداخت نامعتبر است.");
        }

        var employee = await _dbContext.Employees
            .Include(existing => existing.PayPlans)
            .FirstOrDefaultAsync(existing => existing.Id == request.EmployeeId, cancellationToken)
            ?? throw new KeyNotFoundException("Employee was not found.");

        var newPlan = new EmployeePayPlan(
            request.EmployeeId,
            kind,
            request.SalaryAmount,
            request.HourlyRate,
            request.ShiftRate,
            request.CommissionPercent,
            request.OvertimeMultiplier,
            request.Notes);

        _dbContext.EmployeePayPlans.Add(newPlan);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // flip older plans after the new one exists — history is preserved
        await _dbContext.EmployeePayPlans
            .Where(plan => plan.EmployeeId == request.EmployeeId
                && plan.Id != newPlan.Id
                && plan.IsActive)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(plan => plan.IsActive, false),
                cancellationToken);

        return Sugarista.Application.Hr.Employees.CreateEmployee.EmployeeMapper.ToDto(employee, newPlan);
    }
}
