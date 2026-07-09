using Microsoft.EntityFrameworkCore;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Salon.Commands.UpsertSalonTable;

public record UpsertSalonTableCommand(
    Guid? Id,
    string Name,
    int Capacity,
    int DisplayOrder,
    bool IsActive) : IRequest<Guid>;

public class UpsertSalonTableCommandValidator : AbstractValidator<UpsertSalonTableCommand>
{
    public UpsertSalonTableCommandValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(100);
        RuleFor(command => command.Capacity).GreaterThan(0);
        RuleFor(command => command.DisplayOrder).GreaterThanOrEqualTo(0);
    }
}

public class UpsertSalonTableCommandHandler : IRequestHandler<UpsertSalonTableCommand, Guid>
{
    private readonly IApplicationDbContext _dbContext;

    public UpsertSalonTableCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Guid> Handle(UpsertSalonTableCommand request, CancellationToken cancellationToken)
    {
        var normalizedName = request.Name.Trim();
        var duplicateExists = await _dbContext.SalonTables
            .AnyAsync(table => table.Name == normalizedName && table.Id != request.Id, cancellationToken);

        if (duplicateExists)
        {
            throw new InvalidOperationException("A salon table with this name already exists.");
        }

        if (request.Id is { } tableId)
        {
            var table = await _dbContext.SalonTables
                .FirstOrDefaultAsync(existingTable => existingTable.Id == tableId, cancellationToken)
                ?? throw new KeyNotFoundException("Salon table was not found.");

            table.Update(normalizedName, request.Capacity, request.DisplayOrder, request.IsActive);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return table.Id;
        }

        var salonTable = new SalonTable(normalizedName, request.Capacity, request.DisplayOrder, request.IsActive);
        _dbContext.SalonTables.Add(salonTable);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return salonTable.Id;
    }
}
