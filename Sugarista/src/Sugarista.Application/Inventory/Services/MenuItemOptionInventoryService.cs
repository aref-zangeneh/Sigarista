using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.Inventory.Services;
using Sugarista.Application.MenuItems.OptionGroups;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Inventory.Services;

/// <summary>
/// Deducts (and restores) the exact ingredient usage of an order line's
/// per-item options. The usage is frozen in <see cref="OrderLine.OptionsJson"/>
/// at order time, so admin edits to the option definitions afterwards can never
/// change what a placed order consumed.
/// </summary>
internal static class MenuItemOptionInventoryService
{
    internal static async Task DeductForOrderLineAsync(
        IApplicationDbContext dbContext,
        Guid orderId,
        OrderLine line,
        decimal units,
        CancellationToken cancellationToken)
    {
        var snapshot = MenuItemOptionPricing.Deserialize(line.OptionsJson);
        if (snapshot is null || snapshot.Ingredients.Count == 0 || units <= 0)
        {
            return;
        }

        await ApplyAsync(
            dbContext,
            line,
            snapshot,
            units,
            deduct: true,
            orderId,
            cancellationToken);
    }

    internal static async Task RestoreForOrderLineAsync(
        IApplicationDbContext dbContext,
        Guid orderId,
        OrderLine line,
        decimal units,
        CancellationToken cancellationToken)
    {
        var snapshot = MenuItemOptionPricing.Deserialize(line.OptionsJson);
        if (snapshot is null || snapshot.Ingredients.Count == 0 || units <= 0)
        {
            return;
        }

        await ApplyAsync(
            dbContext,
            line,
            snapshot,
            units,
            deduct: false,
            orderId,
            cancellationToken);
    }

    private static async Task ApplyAsync(
        IApplicationDbContext dbContext,
        OrderLine line,
        MenuItemOptionSnapshot snapshot,
        decimal units,
        bool deduct,
        Guid orderId,
        CancellationToken cancellationToken)
    {
        var ingredientIds = snapshot.Ingredients.Select(row => row.IngredientId).ToList();
        var stocks = await dbContext.InventoryStocks
            .Where(stock => ingredientIds.Contains(stock.IngredientId))
            .ToListAsync(cancellationToken);

        var newMovements = new List<StockMovement>();

        foreach (var usage in snapshot.Ingredients)
        {
            var stock = stocks.FirstOrDefault(candidate => candidate.IngredientId == usage.IngredientId);
            if (stock is null)
            {
                continue;
            }

            var amount = usage.Quantity * units;
            var movement = deduct
                ? stock.Deduct(amount, line.PersianName, orderId, line.Id)
                : stock.Restore(amount, line.PersianName, "Option restore", orderId, line.Id);

            if (deduct)
            {
                await InventoryLotAllocator.DeductAsync(dbContext, usage.IngredientId, amount, cancellationToken);
            }
            else
            {
                await InventoryLotAllocator.RestoreAsync(dbContext, usage.IngredientId, amount, cancellationToken);
            }

            newMovements.Add(movement);
        }

        if (newMovements.Count > 0)
        {
            dbContext.StockMovements.AddRange(newMovements);
        }
    }
}
