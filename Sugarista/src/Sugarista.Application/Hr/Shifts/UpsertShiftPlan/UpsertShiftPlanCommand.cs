using Microsoft.EntityFrameworkCore;
using Sugarista.Domain.Entities;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Application.Hr.Shifts.UpsertShiftPlan;

/// <summary>Creates or replaces the approved shift of a person on one weekday.</summary>
public record UpsertShiftPlanCommand(
    Guid EmployeeId,
    int Weekday,
    string StartTime,
    string EndTime) : IRequest<Sugarista.Application.Hr.Shifts.GetShiftPlans.ShiftPlanDto>;

public class UpsertShiftPlanCommandHandler : IRequestHandler<UpsertShiftPlanCommand, Sugarista.Application.Hr.Shifts.GetShiftPlans.ShiftPlanDto>
{
    private readonly IApplicationDbContext _dbContext;

    public UpsertShiftPlanCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Sugarista.Application.Hr.Shifts.GetShiftPlans.ShiftPlanDto> Handle(
        UpsertShiftPlanCommand request,
        CancellationToken cancellationToken)
    {
        if (request.Weekday is < 0 or > 6)
        {
            throw new DomainException("روز هفته نامعتبر است.");
        }

        if (!TimeOnly.TryParse(request.StartTime, out var startTime)
            || !TimeOnly.TryParse(request.EndTime, out var endTime))
        {
            throw new DomainException("فرمت ساعت باید HH:mm باشد.");
        }

        var existing = await _dbContext.EmployeeShiftPlans
            .FirstOrDefaultAsync(plan =>
                plan.EmployeeId == request.EmployeeId && plan.Weekday == request.Weekday,
                cancellationToken);

        if (existing is null)
        {
            var plan = new EmployeeShiftPlan(request.EmployeeId, request.Weekday, startTime, endTime);
            _dbContext.EmployeeShiftPlans.Add(plan);
            await _dbContext.SaveChangesAsync(cancellationToken);

            var employee = await _dbContext.Employees
                .AsNoTracking()
                .FirstAsync(item => item.Id == request.EmployeeId, cancellationToken);

            return new Sugarista.Application.Hr.Shifts.GetShiftPlans.ShiftPlanDto(
                plan.Id,
                plan.EmployeeId,
                employee.PersianName,
                plan.Weekday,
                HrShamsiCalendar.WeekdayLabel(plan.Weekday),
                plan.StartTime.ToString("HH:mm"),
                plan.EndTime.ToString("HH:mm"),
                plan.ScheduledMinutes);
        }

        existing.Update(request.Weekday, startTime, endTime);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var currentEmployee = await _dbContext.Employees
            .AsNoTracking()
            .FirstAsync(item => item.Id == request.EmployeeId, cancellationToken);

        return new Sugarista.Application.Hr.Shifts.GetShiftPlans.ShiftPlanDto(
            existing.Id,
            existing.EmployeeId,
            currentEmployee.PersianName,
            existing.Weekday,
            HrShamsiCalendar.WeekdayLabel(existing.Weekday),
            existing.StartTime.ToString("HH:mm"),
            existing.EndTime.ToString("HH:mm"),
            existing.ScheduledMinutes);
    }
}
