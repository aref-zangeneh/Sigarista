using Microsoft.EntityFrameworkCore;

namespace Sugarista.Application.Salon.Commands.ReleaseSalonTable;

public record ReleaseSalonTableCommand(Guid TableId) : IRequest;

public class ReleaseSalonTableCommandHandler : IRequestHandler<ReleaseSalonTableCommand>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IDateTime _dateTime;

    public ReleaseSalonTableCommandHandler(IApplicationDbContext dbContext, IDateTime dateTime)
    {
        _dbContext = dbContext;
        _dateTime = dateTime;
    }

    public async Task Handle(ReleaseSalonTableCommand request, CancellationToken cancellationToken)
    {
        var session = await _dbContext.TableSessions
            .Where(existingSession => existingSession.TableId == request.TableId && existingSession.EndedAt == null)
            .OrderByDescending(existingSession => existingSession.StartedAt)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("Active table session was not found.");

        session.Release(_dateTime.UtcNow);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
