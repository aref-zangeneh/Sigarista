using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Inventory.Services;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Inventory.Commands.RecordProduction;

public record RecordProductionCommand(
    Guid MenuItemId,
    decimal QuantityProduced,
    string? Note) : IRequest<RecordProductionResultDto>;

public record RecordProductionDeductionDto(
    string IngredientPersianName,
    string Unit,
    decimal AmountDeducted,
    decimal StockAfter,
    bool WentNegative);

public record RecordProductionResultDto(
    Guid ProductionBatchId,
    string MenuItemName,
    decimal QuantityProduced,
    decimal FinishedStockAfter,
    IReadOnlyCollection<RecordProductionDeductionDto> IngredientDeductions);

public class RecordProductionCommandValidator : AbstractValidator<RecordProductionCommand>
{
    public RecordProductionCommandValidator()
    {
        RuleFor(c => c.MenuItemId).NotEmpty();
        RuleFor(c => c.QuantityProduced).GreaterThan(0).WithMessage("Production quantity must be greater than zero.");
        RuleFor(c => c.Note).MaximumLength(1000);
    }
}

public class RecordProductionCommandHandler : IRequestHandler<RecordProductionCommand, RecordProductionResultDto>
{
    private readonly IApplicationDbContext _dbContext;

    public RecordProductionCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<RecordProductionResultDto> Handle(
        RecordProductionCommand request,
        CancellationToken cancellationToken)
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
                $"No recipe is defined for '{menuItem.PersianName}'. Add a recipe before recording production.");
        }

        var batch = new ProductionBatch(request.MenuItemId, request.QuantityProduced, request.Note);
        _dbContext.ProductionBatches.Add(batch);

        var deductions = await RecipeInventoryService.DeductIngredientsForMenuItemAsync(
            _dbContext,
            request.MenuItemId,
            menuItem.PersianName,
            request.QuantityProduced,
            StockMovementType.ProductionDeduction,
            batch.Id,
            cancellationToken: cancellationToken);

        var stock = await _dbContext.MenuItemStocks
            .FirstOrDefaultAsync(s => s.MenuItemId == request.MenuItemId, cancellationToken);

        if (stock is null)
        {
            stock = new MenuItemStock(request.MenuItemId);
            _dbContext.MenuItemStocks.Add(stock);
        }

        stock.AddProduction(request.QuantityProduced);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new RecordProductionResultDto(
            batch.Id,
            menuItem.PersianName,
            request.QuantityProduced,
            stock.CurrentQuantity,
            deductions.Select(d => new RecordProductionDeductionDto(
                d.IngredientPersianName,
                d.Unit,
                d.AmountDeducted,
                d.StockAfter,
                d.WentNegative)).ToList());
    }
}
