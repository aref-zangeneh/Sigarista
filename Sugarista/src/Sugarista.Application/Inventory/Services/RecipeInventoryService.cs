using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.Inventory.Services;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Inventory.Services;

internal static class RecipeInventoryService
{
    internal record IngredientDeductionResult(
        string IngredientPersianName,
        string Unit,
        decimal AmountDeducted,
        decimal StockAfter,
        bool WentNegative);

    internal static async Task<IReadOnlyList<IngredientDeductionResult>> DeductIngredientsForMenuItemAsync(
        IApplicationDbContext dbContext,
        Guid menuItemId,
        string menuItemName,
        decimal units,
        StockMovementType movementType,
        Guid? productionBatchId = null,
        Guid? orderId = null,
        Guid? orderLineId = null,
        CancellationToken cancellationToken = default)
    {
        var recipeLines = await dbContext.RecipeItems
            .AsNoTracking()
            .Where(r => r.MenuItemId == menuItemId)
            .Include(r => r.Ingredient)
            .ToListAsync(cancellationToken);

        if (recipeLines.Count == 0)
        {
            return [];
        }

        var ingredientIds = recipeLines.Select(r => r.IngredientId).ToList();
        var stocks = await dbContext.InventoryStocks
            .Where(s => ingredientIds.Contains(s.IngredientId))
            .ToListAsync(cancellationToken);

        var deductions = new List<IngredientDeductionResult>();
        var newMovements = new List<StockMovement>();

        foreach (var line in recipeLines)
        {
            var stock = stocks.FirstOrDefault(s => s.IngredientId == line.IngredientId);
            if (stock is null)
            {
                continue;
            }

            var totalDeduction = line.Quantity * units;
            var stockBefore = stock.CurrentQuantity;
            StockMovement movement = movementType switch
            {
                StockMovementType.ProductionDeduction when productionBatchId.HasValue =>
                    stock.DeductForProduction(totalDeduction, menuItemName, productionBatchId.Value),
                StockMovementType.SaleDeduction =>
                    stock.Deduct(totalDeduction, menuItemName, orderId, orderLineId),
                _ => stock.Deduct(totalDeduction, menuItemName, orderId, orderLineId),
            };

            await InventoryLotAllocator.DeductAsync(dbContext, line.IngredientId, totalDeduction, cancellationToken);
            newMovements.Add(movement);

            deductions.Add(new IngredientDeductionResult(
                line.Ingredient.PersianName,
                line.Ingredient.Unit,
                totalDeduction,
                stock.CurrentQuantity,
                stockBefore < totalDeduction));
        }

        if (newMovements.Count > 0)
        {
            dbContext.StockMovements.AddRange(newMovements);
        }

        return deductions;
    }

    internal static async Task RestoreIngredientsForMenuItemAsync(
        IApplicationDbContext dbContext,
        Guid menuItemId,
        string menuItemName,
        decimal units,
        string? note = null,
        Guid? orderId = null,
        Guid? orderLineId = null,
        CancellationToken cancellationToken = default)
    {
        var recipeLines = await dbContext.RecipeItems
            .AsNoTracking()
            .Where(r => r.MenuItemId == menuItemId)
            .Include(r => r.Ingredient)
            .ToListAsync(cancellationToken);

        if (recipeLines.Count == 0)
        {
            return;
        }

        var ingredientIds = recipeLines.Select(r => r.IngredientId).ToList();
        var stocks = await dbContext.InventoryStocks
            .Where(s => ingredientIds.Contains(s.IngredientId))
            .ToListAsync(cancellationToken);

        var newMovements = new List<StockMovement>();

        foreach (var recipeLine in recipeLines)
        {
            var stock = stocks.FirstOrDefault(s => s.IngredientId == recipeLine.IngredientId);
            if (stock is null)
            {
                continue;
            }

            var totalRestore = recipeLine.Quantity * units;
            var movement = stock.Restore(
                totalRestore,
                menuItemName,
                note,
                orderId,
                orderLineId);
            await InventoryLotAllocator.RestoreAsync(dbContext, recipeLine.IngredientId, totalRestore, cancellationToken);
            newMovements.Add(movement);
        }

        if (newMovements.Count > 0)
        {
            dbContext.StockMovements.AddRange(newMovements);
        }
    }
}
