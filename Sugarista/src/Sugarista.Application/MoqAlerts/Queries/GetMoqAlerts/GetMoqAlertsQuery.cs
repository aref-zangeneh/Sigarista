using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;

namespace Sugarista.Application.MoqAlerts.Queries.GetMoqAlerts;

public record GetMoqAlertsQuery(
    MoqSourceType? SourceType = null,
    MoqSeverity? Severity = null,
    string? Search = null,
    bool IncludeHealthy = false) : IRequest<MoqAlertSnapshotDto>;

public class GetMoqAlertsQueryHandler : IRequestHandler<GetMoqAlertsQuery, MoqAlertSnapshotDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IMoqAlertBroadcaster _broadcaster;

    public GetMoqAlertsQueryHandler(
        IApplicationDbContext dbContext,
        IMoqAlertBroadcaster broadcaster)
    {
        _dbContext = dbContext;
        _broadcaster = broadcaster;
    }

    public async Task<MoqAlertSnapshotDto> Handle(GetMoqAlertsQuery request, CancellationToken cancellationToken)
    {
        var isUnfilteredAlertSnapshot =
            !request.IncludeHealthy &&
            !request.SourceType.HasValue &&
            !request.Severity.HasValue &&
            string.IsNullOrWhiteSpace(request.Search);

        if (isUnfilteredAlertSnapshot)
        {
            return await _broadcaster.GetOrLoadSnapshotAsync(
                (version, ct) => BuildSnapshotAsync(version, includeHealthy: false, ct),
                cancellationToken);
        }

        var full = await BuildSnapshotAsync(
            _broadcaster.CurrentVersion,
            includeHealthy: request.IncludeHealthy,
            cancellationToken);

        IEnumerable<MoqAlertItemDto> filtered = full.Items;

        if (request.SourceType.HasValue)
        {
            filtered = filtered.Where(item => item.SourceType == request.SourceType.Value);
        }

        if (request.Severity.HasValue)
        {
            filtered = filtered.Where(item => item.Severity == request.Severity.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            filtered = filtered.Where(item =>
                item.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrWhiteSpace(item.ContextLabel) &&
                 item.ContextLabel.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                item.Unit.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        var filteredList = filtered.ToList();
        return full with { Items = filteredList };
    }

    private async Task<MoqAlertSnapshotDto> BuildSnapshotAsync(
        long version,
        bool includeHealthy,
        CancellationToken cancellationToken)
    {
        var items = new List<MoqAlertItemDto>(64);

        // 1. Main Ingredient Inventory (InventoryStock)
        var ingredientStocks = await _dbContext.InventoryStocks
            .AsNoTracking()
            .Where(s => includeHealthy || (s.MinAlertQuantity > 0m && s.CurrentQuantity <= s.MinAlertQuantity))
            .Select(s => new
            {
                s.IngredientId,
                Name = s.Ingredient.PersianName,
                Unit = s.Ingredient.Unit,
                s.CurrentQuantity,
                s.MinAlertQuantity,
                s.LastUpdatedAt,
            })
            .ToListAsync(cancellationToken);

        foreach (var row in ingredientStocks)
        {
            var dto = MoqAlertEvaluator.CreateItem(
                MoqSourceType.Ingredient,
                row.IngredientId,
                null,
                row.Name,
                "انبار اصلی",
                row.Unit,
                row.CurrentQuantity,
                row.MinAlertQuantity,
                row.LastUpdatedAt);

            if (includeHealthy || dto.Severity != MoqSeverity.Healthy)
            {
                items.Add(dto);
            }
        }

        // 2. Menu Item Direct Stock (MenuItemStock)
        var menuStocks = await _dbContext.MenuItemStocks
            .AsNoTracking()
            .Where(s => s.MenuItem.IsActive &&
                        (includeHealthy || (s.MinAlertQuantity > 0m && s.CurrentQuantity <= s.MinAlertQuantity)))
            .Select(s => new
            {
                s.MenuItemId,
                Name = s.MenuItem.PersianName,
                CategoryName = s.MenuItem.Category != null ? s.MenuItem.Category.Name : null,
                s.CurrentQuantity,
                s.MinAlertQuantity,
                s.LastUpdatedAt,
            })
            .ToListAsync(cancellationToken);

        foreach (var row in menuStocks)
        {
            var dto = MoqAlertEvaluator.CreateItem(
                MoqSourceType.MenuItem,
                row.MenuItemId,
                null,
                row.Name,
                row.CategoryName ?? "آیتم منو",
                "عدد",
                row.CurrentQuantity,
                row.MinAlertQuantity,
                row.LastUpdatedAt);

            if (includeHealthy || dto.Severity != MoqSeverity.Healthy)
            {
                items.Add(dto);
            }
        }

        // 3. Sub-Warehouses Stock (WarehouseStock)
        var warehouseStocks = await _dbContext.WarehouseStocks
            .AsNoTracking()
            .Where(s => s.Warehouse.IsActive &&
                        (includeHealthy || (s.MinAlertQuantity > 0m && s.Quantity <= s.MinAlertQuantity)))
            .Select(s => new
            {
                s.IngredientId,
                s.WarehouseId,
                Name = s.Ingredient.PersianName,
                WarehouseName = s.Warehouse.Name,
                Unit = s.Ingredient.Unit,
                s.Quantity,
                s.MinAlertQuantity,
                s.LastUpdatedAt,
            })
            .ToListAsync(cancellationToken);

        foreach (var row in warehouseStocks)
        {
            var dto = MoqAlertEvaluator.CreateItem(
                MoqSourceType.WarehouseIngredient,
                row.IngredientId,
                row.WarehouseId,
                row.Name,
                row.WarehouseName,
                row.Unit,
                row.Quantity,
                row.MinAlertQuantity,
                row.LastUpdatedAt);

            if (includeHealthy || dto.Severity != MoqSeverity.Healthy)
            {
                items.Add(dto);
            }
        }

        // Sort most urgent first: OutOfStock -> Critical -> Warning -> Healthy, then lowest fill %
        var sorted = items
            .OrderByDescending(i => (int)i.Severity)
            .ThenBy(i => i.FillPercent)
            .ThenBy(i => i.Name, StringComparer.Ordinal)
            .ToList();

        var configuredCount = await _dbContext.InventoryStocks.AsNoTracking().CountAsync(s => s.MinAlertQuantity > 0m, cancellationToken)
            + await _dbContext.MenuItemStocks.AsNoTracking().CountAsync(s => s.MenuItem.IsActive && s.MinAlertQuantity > 0m, cancellationToken)
            + await _dbContext.WarehouseStocks.AsNoTracking().CountAsync(s => s.Warehouse.IsActive && s.MinAlertQuantity > 0m, cancellationToken);

        var outOfStock = sorted.Count(i => i.Severity == MoqSeverity.OutOfStock);
        var critical = sorted.Count(i => i.Severity == MoqSeverity.Critical);
        var warning = sorted.Count(i => i.Severity == MoqSeverity.Warning);

        return new MoqAlertSnapshotDto(
            version,
            DateTimeOffset.UtcNow,
            outOfStock + critical + warning,
            outOfStock,
            critical,
            warning,
            configuredCount,
            sorted);
    }
}
