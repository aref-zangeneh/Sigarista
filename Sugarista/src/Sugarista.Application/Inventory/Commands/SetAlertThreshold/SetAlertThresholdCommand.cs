using Microsoft.EntityFrameworkCore;

namespace Sugarista.Application.Inventory.Commands.SetAlertThreshold;

public record SetAlertThresholdCommand(
    Guid IngredientId,
    decimal MinAlertQuantity,
    Guid? WarehouseId = null) : IRequest;

public class SetAlertThresholdCommandValidator : AbstractValidator<SetAlertThresholdCommand>
{
    public SetAlertThresholdCommandValidator()
    {
        RuleFor(c => c.IngredientId).NotEmpty();
        RuleFor(c => c.MinAlertQuantity).GreaterThanOrEqualTo(0);
    }
}

public class SetAlertThresholdCommandHandler : IRequestHandler<SetAlertThresholdCommand>
{
    private readonly IApplicationDbContext _dbContext;

    public SetAlertThresholdCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task Handle(SetAlertThresholdCommand request, CancellationToken cancellationToken)
    {
        if (request.WarehouseId is { } warehouseId)
        {
            var warehouseStock = await _dbContext.WarehouseStocks
                .FirstOrDefaultAsync(
                    s => s.WarehouseId == warehouseId && s.IngredientId == request.IngredientId,
                    cancellationToken)
                ?? throw new KeyNotFoundException("Warehouse stock record was not found for this ingredient.");

            warehouseStock.SetAlertThreshold(request.MinAlertQuantity);

            await _dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        var stock = await _dbContext.InventoryStocks
            .FirstOrDefaultAsync(s => s.IngredientId == request.IngredientId, cancellationToken)
            ?? throw new KeyNotFoundException("Inventory stock record was not found for this ingredient.");

        stock.SetAlertThreshold(request.MinAlertQuantity);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
