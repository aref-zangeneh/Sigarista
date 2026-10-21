using Microsoft.EntityFrameworkCore;

namespace Sugarista.Application.Hr.Employees.SetEmployeeActive;

public record SetEmployeeActiveCommand(Guid Id, bool IsActive, DateOnly? TerminationDate) : IRequest;

public class SetEmployeeActiveCommandHandler : IRequestHandler<SetEmployeeActiveCommand>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IDateTime _dateTime;

    public SetEmployeeActiveCommandHandler(IApplicationDbContext dbContext, IDateTime dateTime)
    {
        _dbContext = dbContext;
        _dateTime = dateTime;
    }

    public async Task Handle(
        SetEmployeeActiveCommand request,
        CancellationToken cancellationToken)
    {
        var employee = await _dbContext.Employees
            .FirstOrDefaultAsync(existing => existing.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Employee was not found.");

        if (request.IsActive)
        {
            employee.Activate();
        }
        else
        {
            employee.Deactivate(request.TerminationDate);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
