using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.Inventory.Services;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Orders.Services;

internal static class OrderInventoryService
{
    internal static async Task DeductInventoryForOrderLinesAsync(
        IApplicationDbContext dbContext,
        Order order,
        IReadOnlyCollection<OrderLine> lines,
        CancellationToken cancellationToken)
    {
        foreach (var line in lines)
        {
            var remainingQuantity = line.Quantity - line.InventoryDeductedQuantity;
            if (remainingQuantity <= 0)
            {
                continue;
            }

            await DeductLineQuantityAsync(dbContext, order.Id, line, remainingQuantity, cancellationToken);
            line.MarkInventoryDeducted(line.InventoryDeductedQuantity + remainingQuantity);
        }
    }

    internal static async Task RestoreInventoryForOrderLinesAsync(
        IApplicationDbContext dbContext,
        Order order,
        IReadOnlyCollection<OrderLine> lines,
        CancellationToken cancellationToken)
    {
        foreach (var line in lines)
        {
            if (line.InventoryDeductedQuantity <= 0)
            {
                continue;
            }

            await RestoreLineQuantityAsync(
                dbContext,
                order.Id,
                line,
                line.InventoryDeductedQuantity,
                cancellationToken);
            line.ResetInventoryDeduction();
        }
    }

    internal static async Task ApplyLineQuantityChangeAsync(
        IApplicationDbContext dbContext,
        Order order,
        OrderLine line,
        int previousQuantity,
        CancellationToken cancellationToken)
    {
        if (line.Quantity == previousQuantity)
        {
            return;
        }

        if (line.Quantity > previousQuantity)
        {
            if (line.InventoryDeductedQuantity > 0)
            {
                var extraQuantity = line.Quantity - previousQuantity;
                await DeductLineQuantityAsync(dbContext, order.Id, line, extraQuantity, cancellationToken);
                line.MarkInventoryDeducted(line.InventoryDeductedQuantity + extraQuantity);
            }

            return;
        }

        var removedQuantity = previousQuantity - line.Quantity;
        var restoreQuantity = Math.Min(removedQuantity, line.InventoryDeductedQuantity);
        if (restoreQuantity <= 0)
        {
            return;
        }

        await RestoreLineQuantityAsync(dbContext, order.Id, line, restoreQuantity, cancellationToken);
        line.MarkInventoryDeducted(line.InventoryDeductedQuantity - restoreQuantity);
    }

    private static async Task DeductLineQuantityAsync(
        IApplicationDbContext dbContext,
        Guid orderId,
        OrderLine line,
        int quantity,
        CancellationToken cancellationToken)
    {
        var recipeLines = await dbContext.RecipeItems
            .AsNoTracking()
            .Where(r => r.MenuItemId == line.MenuItemId)
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

            var totalDeduction = recipeLine.Quantity * quantity;
            var movement = stock.Deduct(totalDeduction, line.PersianName, orderId, line.Id);
            await InventoryLotAllocator.DeductAsync(dbContext, recipeLine.IngredientId, totalDeduction, cancellationToken);
            newMovements.Add(movement);
        }

        if (newMovements.Count > 0)
        {
            dbContext.StockMovements.AddRange(newMovements);
        }
    }

    private static async Task RestoreLineQuantityAsync(
        IApplicationDbContext dbContext,
        Guid orderId,
        OrderLine line,
        int quantity,
        CancellationToken cancellationToken)
    {
        var recipeLines = await dbContext.RecipeItems
            .AsNoTracking()
            .Where(r => r.MenuItemId == line.MenuItemId)
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

            var totalRestore = recipeLine.Quantity * quantity;
            var movement = stock.Restore(
                totalRestore,
                line.PersianName,
                note: $"Order restore {orderId}",
                orderId,
                line.Id);
            await InventoryLotAllocator.RestoreAsync(dbContext, recipeLine.IngredientId, totalRestore, cancellationToken);
            newMovements.Add(movement);
        }

        if (newMovements.Count > 0)
        {
            dbContext.StockMovements.AddRange(newMovements);
        }
    }
}
