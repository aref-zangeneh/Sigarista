using System.Threading.Channels;

namespace Sugarista.Application.MoqAlerts;

public enum MoqSourceType
{
    Ingredient = 0,
    MenuItem = 1,
    WarehouseIngredient = 2
}

public enum MoqSeverity
{
    Healthy = 0,
    Warning = 1,
    Critical = 2,
    OutOfStock = 3
}

public record MoqAlertItemDto(
    string AlertKey,
    MoqSourceType SourceType,
    Guid ItemId,
    Guid? WarehouseId,
    string Name,
    string? ContextLabel,
    string Unit,
    decimal CurrentQuantity,
    decimal MoqQuantity,
    decimal DeficitQuantity,
    decimal FillPercent,
    MoqSeverity Severity,
    DateTimeOffset LastUpdatedAt);

public record MoqAlertSnapshotDto(
    long Version,
    DateTimeOffset GeneratedAt,
    int TotalAlertsCount,
    int OutOfStockCount,
    int CriticalCount,
    int WarningCount,
    int ConfiguredMoqItemsCount,
    IReadOnlyList<MoqAlertItemDto> Items);

public static class MoqAlertEvaluator
{
    public static string BuildAlertKey(MoqSourceType sourceType, Guid itemId, Guid? warehouseId = null)
        => warehouseId.HasValue && warehouseId.Value != Guid.Empty
            ? $"{sourceType}:{itemId:N}:{warehouseId.Value:N}"
            : $"{sourceType}:{itemId:N}";

    public static MoqSeverity Classify(decimal currentQuantity, decimal moqQuantity)
    {
        if (moqQuantity <= 0m)
        {
            return MoqSeverity.Healthy;
        }

        if (currentQuantity <= 0m)
        {
            return MoqSeverity.OutOfStock;
        }

        if (currentQuantity <= moqQuantity * 0.5m)
        {
            return MoqSeverity.Critical;
        }

        if (currentQuantity <= moqQuantity)
        {
            return MoqSeverity.Warning;
        }

        return MoqSeverity.Healthy;
    }

    public static decimal ComputeDeficit(decimal currentQuantity, decimal moqQuantity)
    {
        if (moqQuantity <= 0m || currentQuantity >= moqQuantity)
        {
            return 0m;
        }

        return Math.Round(moqQuantity - currentQuantity, 3);
    }

    public static decimal ComputeFillPercent(decimal currentQuantity, decimal moqQuantity)
    {
        if (moqQuantity <= 0m)
        {
            return 100m;
        }

        if (currentQuantity <= 0m)
        {
            return 0m;
        }

        var pct = Math.Round((currentQuantity / moqQuantity) * 100m, 1);
        return Math.Min(100m, Math.Max(0m, pct));
    }

    public static MoqAlertItemDto CreateItem(
        MoqSourceType sourceType,
        Guid itemId,
        Guid? warehouseId,
        string name,
        string? contextLabel,
        string unit,
        decimal currentQuantity,
        decimal moqQuantity,
        DateTimeOffset lastUpdatedAt)
    {
        var severity = Classify(currentQuantity, moqQuantity);
        return new MoqAlertItemDto(
            BuildAlertKey(sourceType, itemId, warehouseId),
            sourceType,
            itemId,
            warehouseId,
            name,
            contextLabel,
            unit,
            currentQuantity,
            moqQuantity,
            ComputeDeficit(currentQuantity, moqQuantity),
            ComputeFillPercent(currentQuantity, moqQuantity),
            severity,
            lastUpdatedAt);
    }
}

public interface IMoqAlertBroadcaster
{
    long CurrentVersion { get; }

    /// <summary>
    /// Non-blocking signal that stock or MOQ thresholds changed in the DB.
    /// Invalidates any cached snapshot and wakes all SSE subscribers.
    /// </summary>
    void NotifyStockChanged();

    /// <summary>
    /// Registers a subscriber channel for live version notifications.
    /// Disposing the returned token unregisters and completes the channel.
    /// </summary>
    (ChannelReader<long> Reader, IDisposable Subscription) Subscribe();

    /// <summary>
    /// Returns a cached alerts-only snapshot for the current version, or computes
    /// and caches it via the provided loader if stale (coalescing concurrent readers).
    /// </summary>
    Task<MoqAlertSnapshotDto> GetOrLoadSnapshotAsync(
        Func<long, CancellationToken, Task<MoqAlertSnapshotDto>> loader,
        CancellationToken cancellationToken = default);
}
