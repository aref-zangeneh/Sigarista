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
        if (await MenuItemStockService.TryDeductFinishedStockAsync(
                dbContext,
                line.MenuItemId,
                quantity,
                cancellationToken))
        {
            return;
        }

        await RecipeInventoryService.DeductIngredientsForMenuItemAsync(
            dbContext,
            line.MenuItemId,
            line.PersianName,
            quantity,
            StockMovementType.SaleDeduction,
            orderId: orderId,
            orderLineId: line.Id,
            cancellationToken: cancellationToken);
    }

    private static async Task RestoreLineQuantityAsync(
        IApplicationDbContext dbContext,
        Guid orderId,
        OrderLine line,
        int quantity,
        CancellationToken cancellationToken)
    {
        if (await MenuItemStockService.TryRestoreFinishedStockAsync(
                dbContext,
                line.MenuItemId,
                quantity,
                cancellationToken))
        {
            return;
        }

        await RecipeInventoryService.RestoreIngredientsForMenuItemAsync(
            dbContext,
            line.MenuItemId,
            line.PersianName,
            quantity,
            note: $"Order restore {orderId}",
            orderId: orderId,
            orderLineId: line.Id,
            cancellationToken: cancellationToken);
    }
}
