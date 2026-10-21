using Microsoft.EntityFrameworkCore;

namespace Sugarista.Application.Hr.WorkLogs.DeleteEmployeeWorkLog;

public record DeleteEmployeeWorkLogCommand(Guid Id) : IRequest;

public class DeleteEmployeeWorkLogCommandHandler : IRequestHandler<DeleteEmployeeWorkLogCommand>
{
    private readonly IApplicationDbContext _dbContext;

    public DeleteEmployeeWorkLogCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task Handle(
        DeleteEmployeeWorkLogCommand request,
        CancellationToken cancellationToken)
    {
        var log = await _dbContext.EmployeeWorkLogs
            .FirstOrDefaultAsync(existing => existing.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Work log was not found.");

        _dbContext.EmployeeWorkLogs.Remove(log);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
