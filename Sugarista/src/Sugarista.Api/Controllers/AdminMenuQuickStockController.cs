using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sugarista.Api.Authorization;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.Inventory.Services;
using Sugarista.Domain.Constants;
using Sugarista.Domain.Entities;

namespace Sugarista.Api.Controllers;

[Authorize]
[Route("api/admin/menu-quick-stock")]
public class AdminMenuQuickStockController : ApiControllerBase
{
    private const int DefaultExpiringSoonDays = 2;
    private static readonly TimeSpan TehranOffset = TimeSpan.FromHours(3.5);
    private readonly IApplicationDbContext _dbContext;

    public AdminMenuQuickStockController(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    [RequirePermission(AdminPermissions.MenuItems.View)]
    public async Task<ActionResult<QuickStockBoardDto>> GetBoard(
        [FromQuery] Guid? categoryId = null,
        [FromQuery] string? search = null,
        [FromQuery] int expiringWithinDays = DefaultExpiringSoonDays,
        CancellationToken cancellationToken = default)
    {
        var soonDays = Math.Clamp(expiringWithinDays, 1, 14);
        var nowUtc = DateTimeOffset.UtcNow;

        var categories = await _dbContext.Categories
            .AsNoTracking()
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.Name)
            .Select(c => new { c.Id, c.Name, c.IsActive })
            .ToListAsync(cancellationToken);

        var itemsQuery = _dbContext.MenuItems
            .AsNoTracking()
            .Include(m => m.Category)
            .AsQueryable();

        if (categoryId.HasValue)
        {
            itemsQuery = itemsQuery.Where(m => m.CategoryId == categoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            itemsQuery = itemsQuery.Where(m =>
                m.PersianName.ToLower().Contains(term) ||
                m.EnglishName.ToLower().Contains(term) ||
                m.Category.Name.ToLower().Contains(term));
        }

        var menuItems = await itemsQuery
            .OrderBy(m => m.Category.DisplayOrder)
            .ThenBy(m => m.DisplayOrder)
            .ThenBy(m => m.PersianName)
            .ToListAsync(cancellationToken);

        var itemIds = menuItems.Select(m => m.Id).ToList();

        var stocksByItem = await _dbContext.MenuItemStocks
            .AsNoTracking()
            .Where(s => itemIds.Contains(s.MenuItemId))
            .ToDictionaryAsync(s => s.MenuItemId, cancellationToken);

        var activeLots = await _dbContext.MenuItemStockLots
            .AsNoTracking()
            .Where(l => itemIds.Contains(l.MenuItemId) && !l.IsDiscarded && l.RemainingQuantity > 0)
            .ToListAsync(cancellationToken);

        var lotsByItem = activeLots
            .GroupBy(l => l.MenuItemId)
            .ToDictionary(g => g.Key, g => MenuItemStockService.OrderLotsFefo(g));

        var itemDtos = new List<QuickStockItemDto>(menuItems.Count);
        var trackedCount = 0;
        var outOfStockCount = 0;
        var lowStockCount = 0;
        var expiredLotsCount = 0;
        var expiringSoonLotsCount = 0;
        var totalActiveLots = 0;

        foreach (var item in menuItems)
        {
            stocksByItem.TryGetValue(item.Id, out var stock);
            lotsByItem.TryGetValue(item.Id, out var itemLots);
            var dto = BuildItemDto(item, stock, itemLots ?? [], nowUtc, soonDays);
            itemDtos.Add(dto);

            if (dto.HasStockRecord)
            {
                trackedCount++;
                if (dto.CurrentQuantity <= 0)
                {
                    outOfStockCount++;
                }
                else if (dto.CurrentQuantity <= dto.MinAlertQuantity && dto.MinAlertQuantity > 0)
                {
                    lowStockCount++;
                }
            }

            totalActiveLots += dto.Lots.Count;
            expiredLotsCount += dto.Lots.Count(l => l.Status == "expired");
            expiringSoonLotsCount += dto.Lots.Count(l => l.Status == "expiring_soon");
        }

        var countsByCat = menuItems
            .GroupBy(m => m.CategoryId)
            .ToDictionary(g => g.Key, g => g.Count());

        var categoryDtos = categories
            .Select(c => new QuickStockCategoryDto(c.Id, c.Name, c.IsActive, countsByCat.GetValueOrDefault(c.Id, 0)))
            .ToList();

        return Ok(new QuickStockBoardDto(
            new QuickStockSummaryDto(
                itemDtos.Count,
                trackedCount,
                outOfStockCount,
                lowStockCount,
                expiredLotsCount,
                expiringSoonLotsCount,
                totalActiveLots),
            categoryDtos,
            itemDtos));
    }

    [HttpPatch("{menuItemId:guid}/quick-update")]
    [RequirePermission(AdminPermissions.MenuItems.Update)]
    public async Task<ActionResult<QuickStockItemDto>> QuickUpdate(
        Guid menuItemId,
        [FromBody] QuickUpdateMenuItemRequest request,
        CancellationToken cancellationToken)
    {
        var item = await _dbContext.MenuItems
            .Include(m => m.Category)
            .FirstOrDefaultAsync(m => m.Id == menuItemId, cancellationToken);

        if (item is null)
        {
            return NotFound(new { detail = "آیتم منو یافت نشد." });
        }

        var prevSalePrice = item.Price;
        var prevPurchasePrice = item.PurchasePrice;

        // 1. Update pricing & availability if any pricing/availability field is provided
        var newPrice = request.Price ?? item.Price;
        var newSpecialOffer = request.UpdateSpecialOfferPrice ? request.SpecialOfferPrice : item.SpecialOfferPrice;
        var newPurchase = request.UpdatePurchasePrice ? request.PurchasePrice : item.PurchasePrice;

        var newSmallPrice = request.UpdateSizePrices ? request.SmallPrice : item.SmallPrice;
        var newSmallSpecial = request.UpdateSizePrices ? request.SmallSpecialOfferPrice : item.SmallSpecialOfferPrice;
        var newMediumPrice = request.UpdateSizePrices ? request.MediumPrice : item.MediumPrice;
        var newMediumSpecial = request.UpdateSizePrices ? request.MediumSpecialOfferPrice : item.MediumSpecialOfferPrice;
        var newLargePrice = request.UpdateSizePrices ? request.LargePrice : item.LargePrice;
        var newLargeSpecial = request.UpdateSizePrices ? request.LargeSpecialOfferPrice : item.LargeSpecialOfferPrice;

        item.ApplyQuickPricingAndAvailability(
            price: newPrice,
            specialOfferPrice: newSpecialOffer,
            purchasePrice: newPurchase,
            smallPrice: newSmallPrice,
            smallSpecialOfferPrice: newSmallSpecial,
            mediumPrice: newMediumPrice,
            mediumSpecialOfferPrice: newMediumSpecial,
            largePrice: newLargePrice,
            largeSpecialOfferPrice: newLargeSpecial,
            isActive: request.IsActive,
            isOrderable: request.IsOrderable);

        if (prevSalePrice != item.Price || prevPurchasePrice != item.PurchasePrice)
        {
            var changedBy = User?.FindFirstValue(ClaimTypes.Name)
                            ?? User?.FindFirstValue("display_name")
                            ?? User?.Identity?.Name;
            _dbContext.MenuItemPriceHistories.Add(
                MenuItemPriceHistory.Create(
                    item.Id,
                    prevPurchasePrice,
                    item.PurchasePrice,
                    prevSalePrice,
                    item.Price,
                    MenuItemPriceHistory.SourceManualEdit,
                    "ویرایش سریع از صفحه موجودی و قیمت",
                    changedBy));
        }

        // 2. Update stock & keep active lots in sync if quantity/minAlert/trackStock changed
        var stock = await _dbContext.MenuItemStocks
            .FirstOrDefaultAsync(s => s.MenuItemId == menuItemId, cancellationToken);

        var activeLots = await _dbContext.MenuItemStockLots
            .Where(l => l.MenuItemId == menuItemId && !l.IsDiscarded && l.RemainingQuantity > 0)
            .ToListAsync(cancellationToken);

        if (request.TrackStock == false)
        {
            if (stock is not null)
            {
                _dbContext.MenuItemStocks.Remove(stock);
                stock = null;
            }
        }
        else if (request.CurrentQuantity.HasValue || request.MinAlertQuantity.HasValue || request.TrackStock == true)
        {
            if (stock is null)
            {
                stock = new MenuItemStock(menuItemId, request.MinAlertQuantity ?? 0m);
                _dbContext.MenuItemStocks.Add(stock);
            }

            var targetQty = Math.Max(0m, request.CurrentQuantity ?? stock.CurrentQuantity);
            var targetMin = Math.Max(0m, request.MinAlertQuantity ?? stock.MinAlertQuantity);

            // If lots exist and admin adjusted total quantity directly, reconcile lots in FEFO order!
            if (request.CurrentQuantity.HasValue && activeLots.Count > 0)
            {
                var currentLotSum = activeLots.Sum(l => l.RemainingQuantity);
                var delta = targetQty - currentLotSum;

                if (delta < 0)
                {
                    var toDeduct = -delta;
                    foreach (var lot in MenuItemStockService.OrderLotsFefo(activeLots))
                    {
                        if (toDeduct <= 0) break;
                        toDeduct -= lot.Consume(toDeduct);
                    }
                }
                else if (delta > 0)
                {
                    // Add extra units to the newest/latest-expiring lot so older lots keep exact counts
                    var newestLot = activeLots
                        .OrderByDescending(l => l.ExpiresAt ?? DateTimeOffset.MaxValue)
                        .ThenByDescending(l => l.ReceivedAt)
                        .First();
                    newestLot.Restore(delta);
                }
            }

            stock.SetStock(targetQty, targetMin);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var refreshedLots = activeLots
            .Where(l => !l.IsDiscarded && l.RemainingQuantity > 0)
            .ToList();

        return Ok(BuildItemDto(item, stock, MenuItemStockService.OrderLotsFefo(refreshedLots), DateTimeOffset.UtcNow, DefaultExpiringSoonDays));
    }

    [HttpPost("{menuItemId:guid}/lots")]
    [RequirePermission(AdminPermissions.MenuItems.Update)]
    public async Task<ActionResult<QuickStockItemDto>> AddLot(
        Guid menuItemId,
        [FromBody] UpsertStockLotRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Quantity <= 0)
        {
            return BadRequest(new { detail = "تعداد پارت جدید باید بزرگ‌تر از صفر باشد." });
        }

        var item = await _dbContext.MenuItems
            .Include(m => m.Category)
            .FirstOrDefaultAsync(m => m.Id == menuItemId, cancellationToken);

        if (item is null)
        {
            return NotFound(new { detail = "آیتم منو یافت نشد." });
        }

        var stock = await _dbContext.MenuItemStocks
            .FirstOrDefaultAsync(s => s.MenuItemId == menuItemId, cancellationToken);

        var existingActiveLots = await _dbContext.MenuItemStockLots
            .Where(l => l.MenuItemId == menuItemId && !l.IsDiscarded && l.RemainingQuantity > 0)
            .ToListAsync(cancellationToken);

        // Preserve any pre-existing untracked stock count when the very first lot is created
        if (existingActiveLots.Count == 0 && stock is not null && stock.CurrentQuantity > 0 && request.PreserveExistingStock)
        {
            var carriedLot = new MenuItemStockLot(
                menuItemId,
                stock.CurrentQuantity,
                stock.LastUpdatedAt,
                expiresAt: null,
                batchLabel: "موجودی قبلی");
            _dbContext.MenuItemStockLots.Add(carriedLot);
            existingActiveLots.Add(carriedLot);
        }

        var receivedAt = request.ReceivedAt ?? DateTimeOffset.UtcNow;
        var expiresAt = NormalizeExpiryToEndOfTehranDay(request.ExpiresAt);

        var newLot = new MenuItemStockLot(
            menuItemId,
            request.Quantity,
            receivedAt,
            expiresAt,
            request.BatchLabel);

        _dbContext.MenuItemStockLots.Add(newLot);
        existingActiveLots.Add(newLot);

        stock = EnsureAndSyncStockFromLots(menuItemId, stock, existingActiveLots);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(BuildItemDto(item, stock, MenuItemStockService.OrderLotsFefo(existingActiveLots), DateTimeOffset.UtcNow, DefaultExpiringSoonDays));
    }

    [HttpPut("{menuItemId:guid}/lots/{lotId:guid}")]
    [RequirePermission(AdminPermissions.MenuItems.Update)]
    public async Task<ActionResult<QuickStockItemDto>> UpdateLot(
        Guid menuItemId,
        Guid lotId,
        [FromBody] UpsertStockLotRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Quantity < 0)
        {
            return BadRequest(new { detail = "تعداد باقیمانده پارت نمی‌تواند منفی باشد." });
        }

        var item = await _dbContext.MenuItems
            .Include(m => m.Category)
            .FirstOrDefaultAsync(m => m.Id == menuItemId, cancellationToken);

        if (item is null)
        {
            return NotFound(new { detail = "آیتم منو یافت نشد." });
        }

        var lot = await _dbContext.MenuItemStockLots
            .FirstOrDefaultAsync(l => l.Id == lotId && l.MenuItemId == menuItemId, cancellationToken);

        if (lot is null)
        {
            return NotFound(new { detail = "پارت موجودی یافت نشد." });
        }

        var receivedAt = request.ReceivedAt ?? lot.ReceivedAt;
        var expiresAt = request.ClearExpiry
            ? null
            : (request.ExpiresAt.HasValue ? NormalizeExpiryToEndOfTehranDay(request.ExpiresAt) : lot.ExpiresAt);

        lot.UpdateDetails(request.Quantity, receivedAt, expiresAt, request.BatchLabel);

        var allActiveLots = await _dbContext.MenuItemStockLots
            .Where(l => l.MenuItemId == menuItemId && !l.IsDiscarded && l.RemainingQuantity > 0 && l.Id != lotId)
            .ToListAsync(cancellationToken);

        if (lot.IsActive)
        {
            allActiveLots.Add(lot);
        }

        var stock = await _dbContext.MenuItemStocks
            .FirstOrDefaultAsync(s => s.MenuItemId == menuItemId, cancellationToken);

        stock = EnsureAndSyncStockFromLots(menuItemId, stock, allActiveLots);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(BuildItemDto(item, stock, MenuItemStockService.OrderLotsFefo(allActiveLots), DateTimeOffset.UtcNow, DefaultExpiringSoonDays));
    }

    [HttpPost("{menuItemId:guid}/lots/{lotId:guid}/discard")]
    [RequirePermission(AdminPermissions.MenuItems.Update)]
    public async Task<ActionResult<QuickStockItemDto>> DiscardLot(
        Guid menuItemId,
        Guid lotId,
        [FromBody] DiscardStockLotRequest? request,
        CancellationToken cancellationToken)
    {
        var item = await _dbContext.MenuItems
            .Include(m => m.Category)
            .FirstOrDefaultAsync(m => m.Id == menuItemId, cancellationToken);

        if (item is null)
        {
            return NotFound(new { detail = "آیتم منو یافت نشد." });
        }

        var lot = await _dbContext.MenuItemStockLots
            .FirstOrDefaultAsync(l => l.Id == lotId && l.MenuItemId == menuItemId, cancellationToken);

        if (lot is null)
        {
            return NotFound(new { detail = "پارت موجودی یافت نشد." });
        }

        lot.Discard(request?.Reason ?? "ضایعات / انقضا");

        var remainingActiveLots = await _dbContext.MenuItemStockLots
            .Where(l => l.MenuItemId == menuItemId && !l.IsDiscarded && l.RemainingQuantity > 0 && l.Id != lotId)
            .ToListAsync(cancellationToken);

        var stock = await _dbContext.MenuItemStocks
            .FirstOrDefaultAsync(s => s.MenuItemId == menuItemId, cancellationToken);

        stock = EnsureAndSyncStockFromLots(menuItemId, stock, remainingActiveLots);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(BuildItemDto(item, stock, MenuItemStockService.OrderLotsFefo(remainingActiveLots), DateTimeOffset.UtcNow, DefaultExpiringSoonDays));
    }

    [HttpDelete("{menuItemId:guid}/lots/{lotId:guid}")]
    [RequirePermission(AdminPermissions.MenuItems.Update)]
    public async Task<ActionResult<QuickStockItemDto>> DeleteLot(
        Guid menuItemId,
        Guid lotId,
        CancellationToken cancellationToken)
    {
        var item = await _dbContext.MenuItems
            .Include(m => m.Category)
            .FirstOrDefaultAsync(m => m.Id == menuItemId, cancellationToken);

        if (item is null)
        {
            return NotFound(new { detail = "آیتم منو یافت نشد." });
        }

        var lot = await _dbContext.MenuItemStockLots
            .FirstOrDefaultAsync(l => l.Id == lotId && l.MenuItemId == menuItemId, cancellationToken);

        if (lot is null)
        {
            return NotFound(new { detail = "پارت موجودی یافت نشد." });
        }

        _dbContext.MenuItemStockLots.Remove(lot);

        var remainingActiveLots = await _dbContext.MenuItemStockLots
            .Where(l => l.MenuItemId == menuItemId && !l.IsDiscarded && l.RemainingQuantity > 0 && l.Id != lotId)
            .ToListAsync(cancellationToken);

        var stock = await _dbContext.MenuItemStocks
            .FirstOrDefaultAsync(s => s.MenuItemId == menuItemId, cancellationToken);

        if (stock is not null)
        {
            stock.SetStock(remainingActiveLots.Sum(l => l.RemainingQuantity), stock.MinAlertQuantity);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(BuildItemDto(item, stock, MenuItemStockService.OrderLotsFefo(remainingActiveLots), DateTimeOffset.UtcNow, DefaultExpiringSoonDays));
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private MenuItemStock EnsureAndSyncStockFromLots(
        Guid menuItemId,
        MenuItemStock? stock,
        IReadOnlyCollection<MenuItemStockLot> activeLots)
    {
        var total = activeLots.Sum(l => l.RemainingQuantity);
        if (stock is null)
        {
            stock = new MenuItemStock(menuItemId, 0m);
            _dbContext.MenuItemStocks.Add(stock);
        }

        stock.SetStock(total, stock.MinAlertQuantity);
        return stock;
    }

    private static DateTimeOffset? NormalizeExpiryToEndOfTehranDay(DateTimeOffset? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        var raw = value.Value;
        // If caller passed a date-only midnight timestamp, normalize to end of that Tehran calendar day (23:59:59+03:30)
        if (raw.TimeOfDay == TimeSpan.Zero && raw.Offset == TimeSpan.Zero)
        {
            return new DateTimeOffset(raw.Year, raw.Month, raw.Day, 23, 59, 59, TehranOffset).ToUniversalTime();
        }

        return raw.ToUniversalTime();
    }

    private static QuickStockItemDto BuildItemDto(
        MenuItem item,
        MenuItemStock? stock,
        IReadOnlyList<MenuItemStockLot> orderedLots,
        DateTimeOffset nowUtc,
        int expiringSoonDays)
    {
        var todayTehran = nowUtc.ToOffset(TehranOffset).Date;
        var lotDtos = new List<QuickStockLotDto>(orderedLots.Count);
        decimal expiredQty = 0m;
        decimal expiringSoonQty = 0m;
        DateTimeOffset? earliestExpiry = null;

        foreach (var lot in orderedLots)
        {
            var (status, daysUntil) = ClassifyExpiry(lot.ExpiresAt, todayTehran, expiringSoonDays);
            if (lot.ExpiresAt.HasValue && (!earliestExpiry.HasValue || lot.ExpiresAt.Value < earliestExpiry.Value))
            {
                earliestExpiry = lot.ExpiresAt.Value;
            }

            if (status == "expired")
            {
                expiredQty += lot.RemainingQuantity;
            }
            else if (status == "expiring_soon")
            {
                expiringSoonQty += lot.RemainingQuantity;
            }

            lotDtos.Add(new QuickStockLotDto(
                lot.Id,
                lot.InitialQuantity,
                lot.RemainingQuantity,
                lot.ReceivedAt,
                lot.ExpiresAt,
                lot.BatchLabel,
                status,
                daysUntil));
        }

        var overallStatus = expiredQty > 0
            ? "expired"
            : expiringSoonQty > 0
                ? "expiring_soon"
                : earliestExpiry.HasValue
                    ? "fresh"
                    : "none";

        var hasSizes = item.SmallPrice.HasValue || item.MediumPrice.HasValue || item.LargePrice.HasValue;

        return new QuickStockItemDto(
            item.Id,
            item.PersianName,
            item.EnglishName,
            item.CategoryId,
            item.Category?.Name ?? string.Empty,
            item.ImageUrl,
            item.IsActive,
            item.IsOrderable,
            item.Price,
            item.SpecialOfferPrice,
            item.PurchasePrice,
            hasSizes,
            item.SmallPrice,
            item.SmallSpecialOfferPrice,
            item.SmallName,
            item.MediumPrice,
            item.MediumSpecialOfferPrice,
            item.MediumName,
            item.LargePrice,
            item.LargeSpecialOfferPrice,
            item.LargeName,
            stock is not null,
            stock?.CurrentQuantity ?? 0m,
            stock?.MinAlertQuantity ?? 0m,
            stock?.LastUpdatedAt,
            earliestExpiry,
            overallStatus,
            expiredQty,
            expiringSoonQty,
            lotDtos);
    }

    private static (string Status, int? DaysUntilExpiry) ClassifyExpiry(
        DateTimeOffset? expiresAt,
        DateTime todayTehran,
        int soonDays)
    {
        if (!expiresAt.HasValue)
        {
            return ("none", null);
        }

        var expDayTehran = expiresAt.Value.ToOffset(TehranOffset).Date;
        var days = (int)(expDayTehran - todayTehran).TotalDays;

        if (days < 0)
        {
            return ("expired", days);
        }

        if (days <= soonDays)
        {
            return ("expiring_soon", days);
        }

        return ("fresh", days);
    }
}

public record QuickStockBoardDto(
    QuickStockSummaryDto Summary,
    IReadOnlyList<QuickStockCategoryDto> Categories,
    IReadOnlyList<QuickStockItemDto> Items);

public record QuickStockSummaryDto(
    int TotalItems,
    int TrackedItems,
    int OutOfStockCount,
    int LowStockCount,
    int ExpiredLotsCount,
    int ExpiringSoonLotsCount,
    int TotalActiveLots);

public record QuickStockCategoryDto(
    Guid Id,
    string Name,
    bool IsActive,
    int ItemCount);

public record QuickStockLotDto(
    Guid Id,
    decimal InitialQuantity,
    decimal RemainingQuantity,
    DateTimeOffset ReceivedAt,
    DateTimeOffset? ExpiresAt,
    string? BatchLabel,
    string Status,
    int? DaysUntilExpiry);

public record QuickStockItemDto(
    Guid Id,
    string PersianName,
    string EnglishName,
    Guid CategoryId,
    string CategoryName,
    string? ImageUrl,
    bool IsActive,
    bool IsOrderable,
    decimal Price,
    decimal? SpecialOfferPrice,
    decimal? PurchasePrice,
    bool HasSizes,
    decimal? SmallPrice,
    decimal? SmallSpecialOfferPrice,
    string? SmallName,
    decimal? MediumPrice,
    decimal? MediumSpecialOfferPrice,
    string? MediumName,
    decimal? LargePrice,
    decimal? LargeSpecialOfferPrice,
    string? LargeName,
    bool HasStockRecord,
    decimal CurrentQuantity,
    decimal MinAlertQuantity,
    DateTimeOffset? StockLastUpdatedAt,
    DateTimeOffset? EarliestExpiresAt,
    string ExpiryStatus,
    decimal ExpiredQuantity,
    decimal ExpiringSoonQuantity,
    IReadOnlyList<QuickStockLotDto> Lots);

public record QuickUpdateMenuItemRequest(
    decimal? Price = null,
    decimal? SpecialOfferPrice = null,
    bool UpdateSpecialOfferPrice = false,
    decimal? PurchasePrice = null,
    bool UpdatePurchasePrice = false,
    bool UpdateSizePrices = false,
    decimal? SmallPrice = null,
    decimal? SmallSpecialOfferPrice = null,
    decimal? MediumPrice = null,
    decimal? MediumSpecialOfferPrice = null,
    decimal? LargePrice = null,
    decimal? LargeSpecialOfferPrice = null,
    bool? IsActive = null,
    bool? IsOrderable = null,
    bool? TrackStock = null,
    decimal? CurrentQuantity = null,
    decimal? MinAlertQuantity = null);

public record UpsertStockLotRequest(
    decimal Quantity,
    DateTimeOffset? ReceivedAt = null,
    DateTimeOffset? ExpiresAt = null,
    string? BatchLabel = null,
    bool ClearExpiry = false,
    bool PreserveExistingStock = true);

public record DiscardStockLotRequest(string? Reason = null);
