using Microsoft.EntityFrameworkCore;

namespace Sugarista.Application.Salon.Commands.DeleteSalonTable;

public record DeleteSalonTableCommand(Guid Id) : IRequest;

public class DeleteSalonTableCommandHandler : IRequestHandler<DeleteSalonTableCommand>
{
    private readonly IApplicationDbContext _dbContext;

    public DeleteSalonTableCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task Handle(DeleteSalonTableCommand request, CancellationToken cancellationToken)
    {
        var table = await _dbContext.SalonTables
            .Include(existingTable => existingTable.Sessions)
            .FirstOrDefaultAsync(existingTable => existingTable.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Salon table was not found.");

        if (table.Sessions.Any(session => session.EndedAt is null))
        {
            throw new InvalidOperationException("Release the active table session before deleting this table.");
        }

        if (table.Sessions.Count > 0)
        {
            throw new InvalidOperationException("Tables with seating history cannot be deleted. Deactivate the table instead.");
        }

        _dbContext.SalonTables.Remove(table);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
