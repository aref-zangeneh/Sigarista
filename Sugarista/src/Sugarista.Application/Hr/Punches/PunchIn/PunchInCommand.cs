using Microsoft.EntityFrameworkCore;
using Sugarista.Domain.Entities;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Application.Hr.Punches.PunchIn;

public record PunchInCommand(
    Guid EmployeeId,
    string? Note = null) : IRequest<PunchDto>;

public record PunchDto(
    Guid Id,
    Guid EmployeeId,
    string EmployeeName,
    DateOnly WorkDate,
    DateTimeOffset? PunchInAt,
    DateTimeOffset? PunchOutAt,
    string Status,
    decimal WorkedMinutes,
    string? Note);

public static class PunchMapper
{
    public static PunchDto ToDto(PunchAttendance attendance, string employeeName) => new(
        attendance.Id,
        attendance.EmployeeId,
        employeeName,
        attendance.WorkDate,
        attendance.PunchInAt,
        attendance.PunchOutAt,
        attendance.PunchOutAt.HasValue
            ? "Closed"
            : attendance.PunchInAt.HasValue ? "Open" : "Incomplete",
        attendance.WorkedMinutes,
        attendance.Note);
}

public class PunchInCommandHandler : IRequestHandler<PunchInCommand, PunchDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IDateTime _dateTime;

    public PunchInCommandHandler(IApplicationDbContext dbContext, IDateTime dateTime)
    {
        _dbContext = dbContext;
        _dateTime = dateTime;
    }

    public async Task<PunchDto> Handle(
        PunchInCommand request,
        CancellationToken cancellationToken)
    {
        var now = _dateTime.UtcNow;
        var cafeToday = CafeClock.CafeToday(now);

        var employee = await _dbContext.Employees
            .FirstOrDefaultAsync(item => item.Id == request.EmployeeId, cancellationToken)
            ?? throw new KeyNotFoundException("Employee was not found.");

        var existing = await _dbContext.PunchAttendances
            .FirstOrDefaultAsync(attendance =>
                attendance.EmployeeId == request.EmployeeId && attendance.WorkDate == cafeToday,
                cancellationToken);

        if (existing is null)
        {
            var attendance = new PunchAttendance(request.EmployeeId, now, cafeToday, "dashboard", request.Note);
            _dbContext.PunchAttendances.Add(attendance);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return PunchMapper.ToDto(attendance, employee.PersianName);
        }

        existing.PunchIn(now, cafeToday, request.Note);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return PunchMapper.ToDto(existing, employee.PersianName);
    }
}
