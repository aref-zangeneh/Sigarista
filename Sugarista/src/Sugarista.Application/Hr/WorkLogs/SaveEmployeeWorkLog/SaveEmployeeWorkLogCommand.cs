using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Hr.Employees.GetEmployeeDetail;

namespace Sugarista.Application.Hr.WorkLogs.SaveEmployeeWorkLog;

/// <summary>Upserts one day's work record for an employee (unique per employee+date).</summary>
public record SaveEmployeeWorkLogCommand(
    Guid EmployeeId,
    DateOnly WorkDate,
    decimal WorkedHours,
    decimal OvertimeHours,
    int ShiftCount,
    string? Note) : IRequest<WorkLogDto>;

public class SaveEmployeeWorkLogCommandHandler : IRequestHandler<SaveEmployeeWorkLogCommand, WorkLogDto>
{
    private readonly IApplicationDbContext _dbContext;

    public SaveEmployeeWorkLogCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<WorkLogDto> Handle(
        SaveEmployeeWorkLogCommand request,
        CancellationToken cancellationToken)
    {
        var existing = await _dbContext.EmployeeWorkLogs
            .FirstOrDefaultAsync(log =>
                log.EmployeeId == request.EmployeeId && log.WorkDate == request.WorkDate,
                cancellationToken);

        if (existing is null)
        {
            var log = new Domain.Entities.EmployeeWorkLog(
                request.EmployeeId,
                request.WorkDate,
                request.WorkedHours,
                request.OvertimeHours,
                request.ShiftCount,
                request.Note);

            _dbContext.EmployeeWorkLogs.Add(log);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return new WorkLogDto(
                log.Id,
                log.EmployeeId,
                log.WorkDate,
                log.WorkedHours,
                log.OvertimeHours,
                log.ShiftCount,
                log.Note);
        }

        existing.Update(request.WorkDate, request.WorkedHours, request.OvertimeHours, request.ShiftCount, request.Note);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new WorkLogDto(
            existing.Id,
            existing.EmployeeId,
            existing.WorkDate,
            existing.WorkedHours,
            existing.OvertimeHours,
            existing.ShiftCount,
            existing.Note);
    }
}
