using Microsoft.EntityFrameworkCore;

namespace Sugarista.Application.Inventory.Commands.AdjustStock;

public record AdjustStockCommand(
    Guid IngredientId,
    decimal Delta,
    string Note) : IRequest;

public class AdjustStockCommandValidator : AbstractValidator<AdjustStockCommand>
{
    public AdjustStockCommandValidator()
    {
        RuleFor(c => c.IngredientId).NotEmpty();
        RuleFor(c => c.Delta).NotEqual(0).WithMessage("Adjustment delta must not be zero.");
        RuleFor(c => c.Note).NotEmpty().MaximumLength(500);
    }
}

public class AdjustStockCommandHandler : IRequestHandler<AdjustStockCommand>
{
    private readonly IApplicationDbContext _dbContext;

    public AdjustStockCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task Handle(AdjustStockCommand request, CancellationToken cancellationToken)
    {
        var stock = await _dbContext.InventoryStocks
            .FirstOrDefaultAsync(s => s.IngredientId == request.IngredientId, cancellationToken)
            ?? throw new KeyNotFoundException("Inventory stock record was not found for this ingredient.");

        var movement = stock.Adjust(request.Delta, request.Note);
        _dbContext.StockMovements.Add(movement);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
