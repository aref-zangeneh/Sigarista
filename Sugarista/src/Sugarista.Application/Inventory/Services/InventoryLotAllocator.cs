using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Inventory.Services;

internal static class InventoryLotAllocator
{
    internal static async Task DeductAsync(
        IApplicationDbContext dbContext,
        Guid ingredientId,
        decimal quantity,
        CancellationToken cancellationToken)
    {
        if (quantity <= 0)
        {
            return;
        }

        var lots = await dbContext.InventoryPurchases
            .Where(p => p.IngredientId == ingredientId && p.RemainingQuantity > 0)
            .OrderBy(p => p.ExpirationDate)
            .ThenBy(p => p.ReceivedAt)
            .ToListAsync(cancellationToken);

        var remaining = quantity;

        foreach (var lot in lots)
        {
            if (remaining <= 0)
            {
                break;
            }

            var take = Math.Min(lot.RemainingQuantity, remaining);
            lot.Deduct(take);
            remaining -= take;
        }
    }

    internal static async Task RestoreAsync(
        IApplicationDbContext dbContext,
        Guid ingredientId,
        decimal quantity,
        CancellationToken cancellationToken)
    {
        if (quantity <= 0)
        {
            return;
        }

        var lot = await dbContext.InventoryPurchases
            .Where(p => p.IngredientId == ingredientId && p.RemainingQuantity < p.Quantity)
            .OrderByDescending(p => p.ReceivedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (lot is null)
        {
            return;
        }

        lot.Restore(quantity);
    }
}
