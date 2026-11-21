using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.Inventory.Services;
using Sugarista.Application.MenuItems.Bundles;
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
            await MenuItemOptionInventoryService.DeductForOrderLineAsync(
                dbContext,
                order.Id,
                line,
                remainingQuantity,
                cancellationToken);
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
            await MenuItemOptionInventoryService.RestoreForOrderLineAsync(
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
                await MenuItemOptionInventoryService.DeductForOrderLineAsync(
                    dbContext,
                    order.Id,
                    line,
                    extraQuantity,
                    cancellationToken);
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
        await MenuItemOptionInventoryService.RestoreForOrderLineAsync(
            dbContext,
            order.Id,
            line,
            restoreQuantity,
            cancellationToken);
        line.MarkInventoryDeducted(line.InventoryDeductedQuantity - restoreQuantity);
    }

    private static async Task DeductLineQuantityAsync(
        IApplicationDbContext dbContext,
        Guid orderId,
        OrderLine line,
        int quantity,
        CancellationToken cancellationToken)
    {
        var bundleComponents = await ResolveBundleComponentsForInventoryAsync(dbContext, line, cancellationToken);
        if (bundleComponents.Count > 0)
        {
            foreach (var (componentId, componentName, componentQty) in bundleComponents)
            {
                var totalUnits = componentQty * quantity;
                await DeductSingleMenuItemAsync(
                    dbContext,
                    orderId,
                    line.Id,
                    componentId,
                    $"{line.PersianName} ({componentName})",
                    totalUnits,
                    cancellationToken);
            }

            // Also deduct any direct packaging/recipe lines attached to the bundle itself (e.g., gift box).
            await DeductSingleMenuItemAsync(
                dbContext,
                orderId,
                line.Id,
                line.MenuItemId,
                line.PersianName,
                quantity,
                cancellationToken);
            return;
        }

        await DeductSingleMenuItemAsync(
            dbContext,
            orderId,
            line.Id,
            line.MenuItemId,
            line.PersianName,
            quantity,
            cancellationToken);
    }

    private static async Task RestoreLineQuantityAsync(
        IApplicationDbContext dbContext,
        Guid orderId,
        OrderLine line,
        int quantity,
        CancellationToken cancellationToken)
    {
        var bundleComponents = await ResolveBundleComponentsForInventoryAsync(dbContext, line, cancellationToken);
        if (bundleComponents.Count > 0)
        {
            foreach (var (componentId, componentName, componentQty) in bundleComponents)
            {
                var totalUnits = componentQty * quantity;
                await RestoreSingleMenuItemAsync(
                    dbContext,
                    orderId,
                    line.Id,
                    componentId,
                    $"{line.PersianName} ({componentName})",
                    totalUnits,
                    cancellationToken);
            }

            await RestoreSingleMenuItemAsync(
                dbContext,
                orderId,
                line.Id,
                line.MenuItemId,
                line.PersianName,
                quantity,
                cancellationToken);
            return;
        }

        await RestoreSingleMenuItemAsync(
            dbContext,
            orderId,
            line.Id,
            line.MenuItemId,
            line.PersianName,
            quantity,
            cancellationToken);
    }

    private static async Task<IReadOnlyList<(Guid ComponentId, string Name, int Quantity)>> ResolveBundleComponentsForInventoryAsync(
        IApplicationDbContext dbContext,
        OrderLine line,
        CancellationToken cancellationToken)
    {
        var snapshot = BundlePricingService.DeserializeSnapshot(line.BundleSnapshotJson);
        if (snapshot is { Components.Count: > 0 })
        {
            return snapshot.Components
                .Select(c => (c.ComponentMenuItemId, c.PersianName, c.Quantity))
                .ToList();
        }

        var liveComponents = await dbContext.MenuItemBundleItems
            .AsNoTracking()
            .Include(b => b.ComponentMenuItem)
            .Where(b => b.BundleMenuItemId == line.MenuItemId)
            .ToListAsync(cancellationToken);

        return liveComponents
            .Where(b => b.ComponentMenuItem is not null)
            .Select(b => (b.ComponentMenuItemId, b.ComponentMenuItem.PersianName, b.Quantity))
            .ToList();
    }

    private static async Task DeductSingleMenuItemAsync(
        IApplicationDbContext dbContext,
        Guid orderId,
        Guid orderLineId,
        Guid menuItemId,
        string label,
        int quantity,
        CancellationToken cancellationToken)
    {
        if (await MenuItemStockService.TryDeductFinishedStockAsync(
                dbContext,
                menuItemId,
                quantity,
                cancellationToken))
        {
            return;
        }

        await RecipeInventoryService.DeductIngredientsForMenuItemAsync(
            dbContext,
            menuItemId,
            label,
            quantity,
            StockMovementType.SaleDeduction,
            orderId: orderId,
            orderLineId: orderLineId,
            cancellationToken: cancellationToken);
    }

    private static async Task RestoreSingleMenuItemAsync(
        IApplicationDbContext dbContext,
        Guid orderId,
        Guid orderLineId,
        Guid menuItemId,
        string label,
        int quantity,
        CancellationToken cancellationToken)
    {
        if (await MenuItemStockService.TryRestoreFinishedStockAsync(
                dbContext,
                menuItemId,
                quantity,
                cancellationToken))
        {
            return;
        }

        await RecipeInventoryService.RestoreIngredientsForMenuItemAsync(
            dbContext,
            menuItemId,
            label,
            quantity,
            note: $"Order restore {orderId}",
            orderId: orderId,
            orderLineId: orderLineId,
            cancellationToken: cancellationToken);
    }
}
