using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Inventory.Services;

internal static class MenuItemStockService
{
    internal static async Task<MenuItemStock?> GetStockAsync(
        IApplicationDbContext dbContext,
        Guid menuItemId,
        CancellationToken cancellationToken)
    {
        return await dbContext.MenuItemStocks
            .FirstOrDefaultAsync(s => s.MenuItemId == menuItemId, cancellationToken);
    }

    internal static async Task EnsureCanSellAsync(
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
                $"موجودی تولید '{menuItemName}' کافی نیست. موجود: {stock.CurrentQuantity:0.###} — درخواست: {quantity:0.###}. ابتدا تولید ثبت کنید.");
        }
    }

    internal static async Task<bool> TryDeductFinishedStockAsync(
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
                $"موجودی تولید کافی نیست. موجود: {stock.CurrentQuantity:0.###} — درخواست: {quantity:0.###}.");
        }

        stock.DeductForSale(quantity);
        return true;
    }

    internal static async Task<bool> TryRestoreFinishedStockAsync(
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
        return true;
    }
}
