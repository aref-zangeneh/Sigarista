using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Orders.Services;

internal static class OrderInventoryService
{
    internal static async Task DeductInventoryForOrderLinesAsync(
        IApplicationDbContext dbContext,
        IReadOnlyCollection<OrderLine> lines,
        CancellationToken cancellationToken)
    {
        foreach (var line in lines)
        {
            var recipeLines = await dbContext.RecipeItems
                .AsNoTracking()
                .Where(r => r.MenuItemId == line.MenuItemId)
                .Include(r => r.Ingredient)
                .ToListAsync(cancellationToken);

            if (recipeLines.Count == 0)
            {
                continue;
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

                var totalDeduction = recipeLine.Quantity * line.Quantity;
                var movement = stock.Deduct(totalDeduction, line.PersianName);
                newMovements.Add(movement);
            }

            if (newMovements.Count > 0)
            {
                dbContext.StockMovements.AddRange(newMovements);
            }
        }
    }
}
