using Microsoft.EntityFrameworkCore;

namespace Sugarista.Application.Inventory.Commands.RecordSale;

public record RecordSaleCommand(
    Guid MenuItemId,
    int QuantitySold) : IRequest<RecordSaleResultDto>;

public record RecordSaleDeductionDto(
    string IngredientPersianName,
    string Unit,
    decimal AmountDeducted,
    decimal StockAfter,
    bool WentNegative);

public record RecordSaleResultDto(
    string MenuItemName,
    int QuantitySold,
    IReadOnlyCollection<RecordSaleDeductionDto> Deductions);

public class RecordSaleCommandValidator : AbstractValidator<RecordSaleCommand>
{
    public RecordSaleCommandValidator()
    {
        RuleFor(c => c.MenuItemId).NotEmpty();
        RuleFor(c => c.QuantitySold).GreaterThan(0).WithMessage("Quantity sold must be greater than zero.");
    }
}

public class RecordSaleCommandHandler : IRequestHandler<RecordSaleCommand, RecordSaleResultDto>
{
    private readonly IApplicationDbContext _dbContext;

    public RecordSaleCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<RecordSaleResultDto> Handle(RecordSaleCommand request, CancellationToken cancellationToken)
    {
        var menuItem = await _dbContext.MenuItems
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == request.MenuItemId, cancellationToken)
            ?? throw new KeyNotFoundException("Menu item was not found.");

        var recipeLines = await _dbContext.RecipeItems
            .AsNoTracking()
            .Where(r => r.MenuItemId == request.MenuItemId)
            .Include(r => r.Ingredient)
            .ToListAsync(cancellationToken);

        if (recipeLines.Count == 0)
        {
            throw new InvalidOperationException($"No recipe is defined for '{menuItem.PersianName}'. Add a recipe before recording sales.");
        }

        var ingredientIds = recipeLines.Select(r => r.IngredientId).ToList();

        var stocks = await _dbContext.InventoryStocks
            .Where(s => ingredientIds.Contains(s.IngredientId))
            .ToListAsync(cancellationToken);

        var deductions = new List<RecordSaleDeductionDto>();
        var newMovements = new List<Domain.Entities.StockMovement>();

        foreach (var line in recipeLines)
        {
            var stock = stocks.FirstOrDefault(s => s.IngredientId == line.IngredientId);
            if (stock is null)
            {
                continue;
            }

            var totalDeduction = line.Quantity * request.QuantitySold;
            var stockBefore = stock.CurrentQuantity;
            var movement = stock.Deduct(totalDeduction, menuItem.PersianName);
            newMovements.Add(movement);

            deductions.Add(new RecordSaleDeductionDto(
                line.Ingredient.PersianName,
                line.Ingredient.Unit,
                totalDeduction,
                stock.CurrentQuantity,
                stockBefore < totalDeduction));
        }

        _dbContext.StockMovements.AddRange(newMovements);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new RecordSaleResultDto(menuItem.PersianName, request.QuantitySold, deductions);
    }
}
