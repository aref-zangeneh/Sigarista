using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Inventory.Services;

public static class MenuItemStockService
{
    public static async Task<MenuItemStock?> GetStockAsync(
        IApplicationDbContext dbContext,
        Guid menuItemId,
        CancellationToken cancellationToken)
    {
        return await dbContext.MenuItemStocks
            .FirstOrDefaultAsync(s => s.MenuItemId == menuItemId, cancellationToken);
    }

    public static async Task EnsureCanSellAsync(
        IApplicationDbContext dbContext,
        Guid menuItemId,
        string menuItemName,
        decimal quantity,
        CancellationToken cancellationToken)
    {
        var stock = await GetStockAsync(dbContext, menuItemId, cancellationToken);
        if (stock is null)
        {
            return;
        }

        if (stock.CurrentQuantity < quantity)
        {
            throw new InvalidOperationException(
                $"موجودی '{menuItemName}' کافی نیست. موجود: {stock.CurrentQuantity:0.###} — درخواست: {quantity:0.###}.");
        }
    }

    public static async Task<bool> TryDeductFinishedStockAsync(
        IApplicationDbContext dbContext,
        Guid menuItemId,
        decimal quantity,
        CancellationToken cancellationToken)
    {
        var stock = await GetStockAsync(dbContext, menuItemId, cancellationToken);
        if (stock is null)
        {
            return false;
        }

        if (stock.CurrentQuantity < quantity)
        {
            throw new InvalidOperationException(
                $"موجودی کافی نیست. موجود: {stock.CurrentQuantity:0.###} — درخواست: {quantity:0.###}.");
        }

        stock.DeductForSale(quantity);
        await DeductFromLotsFefoAsync(dbContext, menuItemId, quantity, cancellationToken);
        return true;
    }

    public static async Task<bool> TryRestoreFinishedStockAsync(
        IApplicationDbContext dbContext,
        Guid menuItemId,
        decimal quantity,
        CancellationToken cancellationToken)
    {
        var stock = await GetStockAsync(dbContext, menuItemId, cancellationToken);
        if (stock is null)
        {
            return false;
        }

        stock.RestoreFromSale(quantity);
        await RestoreToLotsAsync(dbContext, menuItemId, quantity, cancellationToken);
        return true;
    }

    /// <summary>
    /// Deducts <paramref name="quantity"/> across active lots using strict FEFO + FIFO:
    /// lots with an expiration date are consumed first (earliest <c>ExpiresAt</c> first),
    /// followed by lots without an expiration date, with earliest <c>ReceivedAt</c> / <c>CreatedAt</c> as tie-breaker.
    /// </summary>
    public static async Task DeductFromLotsFefoAsync(
        IApplicationDbContext dbContext,
        Guid menuItemId,
        decimal quantity,
        CancellationToken cancellationToken)
    {
        if (quantity <= 0)
        {
            return;
        }

        var activeLots = await dbContext.MenuItemStockLots
            .Where(l => l.MenuItemId == menuItemId && !l.IsDiscarded && l.RemainingQuantity > 0)
            .ToListAsync(cancellationToken);

        if (activeLots.Count == 0)
        {
            return;
        }

        var ordered = OrderLotsFefo(activeLots);
        var remainingToTake = quantity;

        foreach (var lot in ordered)
        {
            if (remainingToTake <= 0)
            {
                break;
            }

            var taken = lot.Consume(remainingToTake);
            remainingToTake -= taken;
        }
    }

    /// <summary>
    /// Restores <paramref name="quantity"/> to the freshest non-discarded lot when an order is cancelled/refunded.
    /// </summary>
    public static async Task RestoreToLotsAsync(
        IApplicationDbContext dbContext,
        Guid menuItemId,
        decimal quantity,
        CancellationToken cancellationToken)
    {
        if (quantity <= 0)
        {
            return;
        }

        var candidateLots = await dbContext.MenuItemStockLots
            .Where(l => l.MenuItemId == menuItemId && !l.IsDiscarded)
            .ToListAsync(cancellationToken);

        if (candidateLots.Count == 0)
        {
            return;
        }

        // Prefer the lot with the latest expiration date (or latest received date)
        var target = candidateLots
            .OrderByDescending(l => l.ExpiresAt.HasValue ? 1 : 0)
            .ThenByDescending(l => l.ExpiresAt ?? DateTimeOffset.MinValue)
            .ThenByDescending(l => l.ReceivedAt)
            .ThenByDescending(l => l.CreatedAt)
            .First();

        target.Restore(quantity);
    }

    public static IReadOnlyList<MenuItemStockLot> OrderLotsFefo(IEnumerable<MenuItemStockLot> lots) =>
        lots
            .OrderBy(l => l.ExpiresAt.HasValue ? 0 : 1)
            .ThenBy(l => l.ExpiresAt ?? DateTimeOffset.MaxValue)
            .ThenBy(l => l.ReceivedAt)
            .ThenBy(l => l.CreatedAt)
            .ToList();
}
