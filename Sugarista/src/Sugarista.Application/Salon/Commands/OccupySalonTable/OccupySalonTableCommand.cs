using Microsoft.EntityFrameworkCore;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Salon.Commands.OccupySalonTable;

public record OccupySalonTableCommand(Guid TableId, int PeopleCount, string? Note) : IRequest<Guid>;

public class OccupySalonTableCommandValidator : AbstractValidator<OccupySalonTableCommand>
{
    public OccupySalonTableCommandValidator()
    {
        RuleFor(command => command.TableId).NotEmpty();
        RuleFor(command => command.PeopleCount).GreaterThan(0);
        RuleFor(command => command.Note).MaximumLength(500);
    }
}

public class OccupySalonTableCommandHandler : IRequestHandler<OccupySalonTableCommand, Guid>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IDateTime _dateTime;

    public OccupySalonTableCommandHandler(IApplicationDbContext dbContext, IDateTime dateTime)
    {
        _dbContext = dbContext;
        _dateTime = dateTime;
    }

    public async Task<Guid> Handle(OccupySalonTableCommand request, CancellationToken cancellationToken)
    {
        var table = await _dbContext.SalonTables
            .FirstOrDefaultAsync(existingTable => existingTable.Id == request.TableId, cancellationToken)
            ?? throw new KeyNotFoundException("Salon table was not found.");

        if (!table.IsActive)
        {
            throw new InvalidOperationException("Inactive tables cannot be occupied.");
        }

        if (request.PeopleCount > table.Capacity)
        {
            throw new InvalidOperationException("People count cannot be greater than table capacity.");
        }

        var alreadyOccupied = await _dbContext.TableSessions
            .AnyAsync(session => session.TableId == request.TableId && session.EndedAt == null, cancellationToken);

        if (alreadyOccupied)
        {
            throw new InvalidOperationException("This table is already occupied.");
        }

        var session = new TableSession(request.TableId, request.PeopleCount, _dateTime.UtcNow, request.Note);
        _dbContext.TableSessions.Add(session);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return session.Id;
    }
}
