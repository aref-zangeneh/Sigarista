using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Hr.Punches.PunchIn;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Application.Hr.Punches.PunchOut;

public record PunchOutCommand(Guid EmployeeId) : IRequest<PunchDto>;

public class PunchOutCommandHandler : IRequestHandler<PunchOutCommand, PunchDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IDateTime _dateTime;

    public PunchOutCommandHandler(IApplicationDbContext dbContext, IDateTime dateTime)
    {
        _dbContext = dbContext;
        _dateTime = dateTime;
    }

    public async Task<PunchDto> Handle(
        PunchOutCommand request,
        CancellationToken cancellationToken)
    {
        var now = _dateTime.UtcNow;

        var employee = await _dbContext.Employees
            .FirstOrDefaultAsync(item => item.Id == request.EmployeeId, cancellationToken)
            ?? throw new KeyNotFoundException("Employee was not found.");

        // the open punch of today — or an overnight punch still open from yesterday
        var open = await _dbContext.PunchAttendances
            .Where(attendance => attendance.EmployeeId == request.EmployeeId
                && attendance.PunchInAt != null
                && attendance.PunchOutAt == null)
            .OrderByDescending(attendance => attendance.PunchInAt)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new DomainException("پانچ بازی برای خروج وجود ندارد؛ ابتدا ورود را ثبت کنید.");

        open.PunchOut(now);

        if (CafeClock.CafeToday(now) != open.WorkDate)
        {
            open.SetNote($"گذر از نیمه‌شب — روز {open.WorkDate:yyyy-MM-dd}");
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return PunchMapper.ToDto(open, employee.PersianName);
    }
}
