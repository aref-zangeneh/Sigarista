using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Inventory.Services;

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
    bool DeductedFromProducedStock,
    decimal? ProducedStockAfter,
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

        var hasRecipe = await _dbContext.RecipeItems
            .AnyAsync(r => r.MenuItemId == request.MenuItemId, cancellationToken);

        if (!hasRecipe)
        {
            throw new InvalidOperationException(
                $"No recipe is defined for '{menuItem.PersianName}'. Add a recipe before recording sales.");
        }

        var soldFromStock = await MenuItemStockService.TryDeductFinishedStockAsync(
            _dbContext,
            request.MenuItemId,
            request.QuantitySold,
            cancellationToken);

        if (soldFromStock)
        {
            var stock = await MenuItemStockService.GetStockAsync(_dbContext, request.MenuItemId, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return new RecordSaleResultDto(
                menuItem.PersianName,
                request.QuantitySold,
                true,
                stock?.CurrentQuantity,
                []);
        }

        var deductions = await RecipeInventoryService.DeductIngredientsForMenuItemAsync(
            _dbContext,
            request.MenuItemId,
            menuItem.PersianName,
            request.QuantitySold,
            Domain.Entities.StockMovementType.SaleDeduction,
            cancellationToken: cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new RecordSaleResultDto(
            menuItem.PersianName,
            request.QuantitySold,
            false,
            null,
            deductions.Select(d => new RecordSaleDeductionDto(
                d.IngredientPersianName,
                d.Unit,
                d.AmountDeducted,
                d.StockAfter,
                d.WentNegative)).ToList());
    }
}
