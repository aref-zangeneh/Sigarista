using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.Inventory.Services;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.DeliveryMethods.Services;

/// <summary>
/// Deducts (and restores) a delivery method's disposable packaging set from stock.
/// Deduction is idempotent per order via Order.PackagingDeducted.
/// </summary>
internal static class DeliveryMethodInventoryService
{
    /// <summary>Number of packaging units deducted per order. Might go negative; waste is tracked.</summary>
    internal static async Task DeductForOrderAsync(
        IApplicationDbContext dbContext,
        Domain.Entities.Order order,
        CancellationToken cancellationToken = default)
    {
        if (order.PackagingDeducted || order.DeliveryMethodId is not { } methodId)
        {
            return;
        }

        var method = await dbContext.DeliveryMethods
            .AsNoTracking()
            .Include(m => m.Packagings)
            .ThenInclude(line => line.Ingredient)
            .FirstOrDefaultAsync(m => m.Id == methodId, cancellationToken);

        if (method is null || method.Packagings.Count == 0)
        {
            return;
        }

        var ingredientIds = method.Packagings.Select(line => line.IngredientId).ToList();
        var stocks = await dbContext.InventoryStocks
            .Where(stock => ingredientIds.Contains(stock.IngredientId))
            .ToListAsync(cancellationToken);

        var newMovements = new List<StockMovement>();

        foreach (var line in method.Packagings)
        {
            var stock = stocks.FirstOrDefault(s => s.IngredientId == line.IngredientId);
            if (stock is null)
            {
                continue;
            }

            var movement = stock.DeductForPackaging(line.QuantityPerOrder, method.PersianName, order.Id);
            await InventoryLotAllocator.DeductAsync(dbContext, line.IngredientId, line.QuantityPerOrder, cancellationToken);
            newMovements.Add(movement);
        }

        if (newMovements.Count > 0)
        {
            dbContext.StockMovements.AddRange(newMovements);
        }

        order.SetPackagingDeducted(true);
    }

    internal static async Task RestoreForOrderAsync(
        IApplicationDbContext dbContext,
        Domain.Entities.Order order,
        CancellationToken cancellationToken = default)
    {
        if (!order.PackagingDeducted || order.DeliveryMethodId is not { } methodId)
        {
            return;
        }

        var method = await dbContext.DeliveryMethods
            .AsNoTracking()
            .Include(m => m.Packagings)
            .ThenInclude(line => line.Ingredient)
            .FirstOrDefaultAsync(m => m.Id == methodId, cancellationToken);

        if (method is null || method.Packagings.Count == 0)
        {
            order.SetPackagingDeducted(false);
            return;
        }

        var ingredientIds = method.Packagings.Select(line => line.IngredientId).ToList();
        var stocks = await dbContext.InventoryStocks
            .Where(stock => ingredientIds.Contains(stock.IngredientId))
            .ToListAsync(cancellationToken);

        var newMovements = new List<StockMovement>();

        foreach (var line in method.Packagings)
        {
            var stock = stocks.FirstOrDefault(s => s.IngredientId == line.IngredientId);
            if (stock is null)
            {
                continue;
            }

            var movement = stock.Restore(
                line.QuantityPerOrder,
                method.PersianName,
                note: $"بازگشت بسته‌بندی تحویل — {method.PersianName}",
                orderId: order.Id);
            await InventoryLotAllocator.RestoreAsync(dbContext, line.IngredientId, line.QuantityPerOrder, cancellationToken);
            newMovements.Add(movement);
        }

        if (newMovements.Count > 0)
        {
            dbContext.StockMovements.AddRange(newMovements);
        }

        order.SetPackagingDeducted(false);
    }
}
