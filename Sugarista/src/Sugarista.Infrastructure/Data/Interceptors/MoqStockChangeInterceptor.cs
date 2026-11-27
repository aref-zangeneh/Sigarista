using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Sugarista.Application.MoqAlerts;
using Sugarista.Domain.Entities;

namespace Sugarista.Infrastructure.Data.Interceptors;

/// <summary>
/// Intercepts EF Core SaveChanges calls and triggers an instantaneous,
/// non-blocking real-time MOQ alert broadcast whenever any InventoryStock,
/// MenuItemStock, or WarehouseStock record is created, updated, or deleted.
/// </summary>
public sealed class MoqStockChangeInterceptor : SaveChangesInterceptor
{
    private readonly IMoqAlertBroadcaster _broadcaster;
    private bool _hasPendingStockChanges;

    public MoqStockChangeInterceptor(IMoqAlertBroadcaster broadcaster)
    {
        _broadcaster = broadcaster;
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        _hasPendingStockChanges = DetectStockChanges(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        _hasPendingStockChanges = DetectStockChanges(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        FlushNotification();
        return base.SavedChanges(eventData, result);
    }

    public override ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        FlushNotification();
        return base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        _hasPendingStockChanges = false;
        base.SaveChangesFailed(eventData);
    }

    public override Task SaveChangesFailedAsync(
        DbContextErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        _hasPendingStockChanges = false;
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    private void FlushNotification()
    {
        if (!_hasPendingStockChanges)
        {
            return;
        }

        _hasPendingStockChanges = false;
        _broadcaster.NotifyStockChanged();
    }

    private static bool DetectStockChanges(DbContext? context)
    {
        if (context is null)
        {
            return false;
        }

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
            {
                continue;
            }

            if (entry.Entity is InventoryStock or MenuItemStock or WarehouseStock)
            {
                return true;
            }
        }

        return false;
    }
}
