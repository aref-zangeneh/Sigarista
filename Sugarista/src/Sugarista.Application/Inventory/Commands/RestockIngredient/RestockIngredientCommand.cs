using Microsoft.EntityFrameworkCore;

namespace Sugarista.Application.Inventory.Commands.RestockIngredient;

public record RestockIngredientCommand(
    Guid IngredientId,
    decimal Quantity,
    string Note,
    Guid? WarehouseId = null) : IRequest;

public class RestockIngredientCommandValidator : AbstractValidator<RestockIngredientCommand>
{
    public RestockIngredientCommandValidator()
    {
        RuleFor(c => c.IngredientId).NotEmpty();
        RuleFor(c => c.Quantity).GreaterThan(0).WithMessage("Restock quantity must be greater than zero.");
        RuleFor(c => c.Note).MaximumLength(500);
    }
}

public class RestockIngredientCommandHandler : IRequestHandler<RestockIngredientCommand>
{
    private readonly IApplicationDbContext _dbContext;

    public RestockIngredientCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task Handle(RestockIngredientCommand request, CancellationToken cancellationToken)
    {
        if (request.WarehouseId is { } warehouseId)
        {
            // Warehouse stock lives in its own table; the legacy per-ingredient
            // stock row is never touched from here.
            var warehouseExists = await _dbContext.Warehouses
                .AsNoTracking()
                .AnyAsync(w => w.Id == warehouseId, cancellationToken);

            if (!warehouseExists)
            {
                throw new KeyNotFoundException("Warehouse was not found.");
            }

            var warehouseStock = await _dbContext.WarehouseStocks
                .FirstOrDefaultAsync(
                    s => s.WarehouseId == warehouseId && s.IngredientId == request.IngredientId,
                    cancellationToken);

            if (warehouseStock is null)
            {
                warehouseStock = new Domain.Entities.WarehouseStock(warehouseId, request.IngredientId);
                _dbContext.WarehouseStocks.Add(warehouseStock);
            }

            var warehouseMovement = warehouseStock.Restock(request.Quantity, request.Note);
            _dbContext.StockMovements.Add(warehouseMovement);

            await _dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        var stock = await _dbContext.InventoryStocks
            .FirstOrDefaultAsync(s => s.IngredientId == request.IngredientId, cancellationToken)
            ?? throw new KeyNotFoundException("Inventory stock record was not found for this ingredient.");

        var movement = stock.Restock(request.Quantity, request.Note);
        _dbContext.StockMovements.Add(movement);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
