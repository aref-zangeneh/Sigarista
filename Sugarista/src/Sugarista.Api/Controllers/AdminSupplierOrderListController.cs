using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sugarista.Api.Authorization;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Domain.Constants;
using Sugarista.Domain.Entities;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Api.Controllers;

/// <summary>
/// Manages the cafe's Supplier Restock Order List (OrderList):
/// collecting out-of-stock / low-stock menu items (cakes, pastries, desserts, or ad-hoc supplies),
/// grouping by supplier, exporting cleanly to WhatsApp / PDF, and tracking Ordered -> Received lifecycle.
/// </summary>
[Authorize]
[Route("api/admin/supplier-order-list")]
public class AdminSupplierOrderListController : ApiControllerBase
{
    private readonly IApplicationDbContext _dbContext;

    public AdminSupplierOrderListController(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    [RequirePermission(AdminPermissions.OrderList.View)]
    public async Task<ActionResult<SupplierOrderListResponseDto>> GetList(
        [FromQuery] SupplierOrderListItemStatus? status = null,
        [FromQuery] Guid? supplierId = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var allItems = await _dbContext.SupplierOrderListItems
            .AsNoTracking()
            .Include(x => x.Supplier)
            .Include(x => x.MenuItem)
                .ThenInclude(m => m!.Category)
            .OrderBy(x => x.Status)
            .ThenByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        var pendingCount = allItems.Count(x => x.Status == SupplierOrderListItemStatus.Pending);
        var orderedCount = allItems.Count(x => x.Status == SupplierOrderListItemStatus.Ordered);
        var receivedCount = allItems.Count(x => x.Status == SupplierOrderListItemStatus.Received);

        IEnumerable<SupplierOrderListItem> filtered = allItems;

        if (status.HasValue)
        {
            filtered = filtered.Where(x => x.Status == status.Value);
        }
        else
        {
            filtered = filtered.Where(x => x.Status != SupplierOrderListItemStatus.Cancelled);
        }

        if (supplierId.HasValue)
        {
            filtered = filtered.Where(x => x.SupplierId == supplierId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            filtered = filtered.Where(x =>
                x.ItemName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (x.CategoryName != null && x.CategoryName.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                (x.Supplier != null && x.Supplier.PersianName.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                (x.SupplierNameOverride != null && x.SupplierNameOverride.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                (x.Notes != null && x.Notes.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                (x.BatchCode != null && x.BatchCode.Contains(term, StringComparison.OrdinalIgnoreCase)));
        }

        var filteredList = filtered.ToList();
        var menuItemIds = filteredList
            .Where(x => x.MenuItemId.HasValue)
            .Select(x => x.MenuItemId!.Value)
            .Distinct()
            .ToList();

        var liveStocks = menuItemIds.Count > 0
            ? await _dbContext.MenuItemStocks
                .AsNoTracking()
                .Where(s => menuItemIds.Contains(s.MenuItemId))
                .ToDictionaryAsync(s => s.MenuItemId, cancellationToken)
            : new Dictionary<Guid, MenuItemStock>();

        var dtos = filteredList
            .Select(x => MapDto(x, x.MenuItemId.HasValue && liveStocks.TryGetValue(x.MenuItemId.Value, out var st) ? st : null))
            .ToList();

        return Ok(new SupplierOrderListResponseDto(
            PendingCount: pendingCount,
            OrderedCount: orderedCount,
            ReceivedCount: receivedCount,
            Items: dtos));
    }

    [HttpGet("pending-count")]
    [RequirePermission(AdminPermissions.OrderList.View)]
    public async Task<ActionResult<object>> GetPendingCount(CancellationToken cancellationToken = default)
    {
        var count = await _dbContext.SupplierOrderListItems
            .AsNoTracking()
            .CountAsync(x => x.Status == SupplierOrderListItemStatus.Pending, cancellationToken);

        return Ok(new { pendingCount = count });
    }

    [HttpGet("low-stock-suggestions")]
    [RequirePermission(AdminPermissions.OrderList.View)]
    public async Task<ActionResult<IReadOnlyList<LowStockSuggestionDto>>> GetLowStockSuggestions(
        CancellationToken cancellationToken = default)
    {
        var lowStocks = await _dbContext.MenuItemStocks
            .AsNoTracking()
            .Include(s => s.MenuItem)
                .ThenInclude(m => m.Category)
            .Where(s => s.MenuItem.IsActive && (s.CurrentQuantity <= 0 || s.CurrentQuantity <= s.MinAlertQuantity))
            .OrderBy(s => s.CurrentQuantity)
            .ThenBy(s => s.MenuItem.PersianName)
            .ToListAsync(cancellationToken);

        var pendingMenuItemIds = (await _dbContext.SupplierOrderListItems
                .AsNoTracking()
                .Where(x => x.Status == SupplierOrderListItemStatus.Pending && x.MenuItemId != null)
                .Select(x => x.MenuItemId!.Value)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        var suggestions = lowStocks.Select(s => new LowStockSuggestionDto(
            MenuItemId: s.MenuItemId,
            ItemName: s.MenuItem.PersianName,
            CategoryName: s.MenuItem.Category?.Name ?? string.Empty,
            CurrentQuantity: s.CurrentQuantity,
            MinAlertQuantity: s.MinAlertQuantity,
            SuggestedOrderQuantity: Math.Max(1, Math.Ceiling(Math.Max(s.MinAlertQuantity * 2, 4) - s.CurrentQuantity)),
            IsOutOfStock: s.CurrentQuantity <= 0,
            AlreadyInOrderList: pendingMenuItemIds.Contains(s.MenuItemId)))
            .ToList();

        return Ok(suggestions);
    }

    [HttpPost]
    [RequirePermission(AdminPermissions.OrderList.Create)]
    public async Task<ActionResult<SupplierOrderListItemDto>> AddItem(
        [FromBody] AddSupplierOrderListItemRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            MenuItem? menuItem = null;
            MenuItemStock? stock = null;

            if (request.MenuItemId.HasValue)
            {
                menuItem = await _dbContext.MenuItems
                    .AsNoTracking()
                    .Include(m => m.Category)
                    .FirstOrDefaultAsync(m => m.Id == request.MenuItemId.Value, cancellationToken);

                if (menuItem is null)
                {
                    return NotFound(new { message = "آیتم منوی انتخاب‌شده پیدا نشد." });
                }

                stock = await _dbContext.MenuItemStocks
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.MenuItemId == menuItem.Id, cancellationToken);
            }

            if (request.SupplierId.HasValue)
            {
                var supplierExists = await _dbContext.Suppliers
                    .AsNoTracking()
                    .AnyAsync(s => s.Id == request.SupplierId.Value, cancellationToken);

                if (!supplierExists)
                {
                    return BadRequest(new { message = "تأمین‌کننده انتخاب‌شده معتبر نیست." });
                }
            }

            var resolvedName = !string.IsNullOrWhiteSpace(request.ItemName)
                ? request.ItemName.Trim()
                : menuItem?.PersianName ?? string.Empty;

            var qty = request.Quantity > 0 ? request.Quantity : 1;
            var unit = string.IsNullOrWhiteSpace(request.Unit) ? "عدد" : request.Unit.Trim();
            var stockSnapshot = stock is not null ? (int)Math.Floor(stock.CurrentQuantity) : (int?)null;

            if (request.MergeIfPending && request.MenuItemId.HasValue)
            {
                var existing = await _dbContext.SupplierOrderListItems
                    .Include(x => x.Supplier)
                    .Include(x => x.MenuItem)
                        .ThenInclude(m => m!.Category)
                    .FirstOrDefaultAsync(
                        x => x.Status == SupplierOrderListItemStatus.Pending &&
                             x.MenuItemId == request.MenuItemId.Value,
                        cancellationToken);

                if (existing is not null)
                {
                    existing.AddQuantity(qty, stockSnapshot);
                    if (request.SupplierId.HasValue || !string.IsNullOrWhiteSpace(request.Notes))
                    {
                        existing.UpdateDetails(
                            existing.ItemName,
                            existing.Quantity,
                            existing.Unit,
                            request.SupplierId ?? existing.SupplierId,
                            request.SupplierNameOverride ?? existing.SupplierNameOverride,
                            request.SupplierPhoneOverride ?? existing.SupplierPhoneOverride,
                            !string.IsNullOrWhiteSpace(request.Notes) ? request.Notes : existing.Notes);
                    }

                    await _dbContext.SaveChangesAsync(cancellationToken);

                    var reloaded = await LoadSingleAsync(existing.Id, cancellationToken);
                    return Ok(MapDto(reloaded!, stock));
                }
            }

            var entity = new SupplierOrderListItem(
                itemName: resolvedName,
                quantity: qty,
                unit: unit,
                menuItemId: menuItem?.Id,
                categoryName: request.CategoryName ?? menuItem?.Category?.Name,
                supplierId: request.SupplierId,
                supplierNameOverride: request.SupplierNameOverride,
                supplierPhoneOverride: request.SupplierPhoneOverride,
                notes: request.Notes,
                currentStockSnapshot: stockSnapshot);

            _dbContext.SupplierOrderListItems.Add(entity);
            await _dbContext.SaveChangesAsync(cancellationToken);

            var created = await LoadSingleAsync(entity.Id, cancellationToken);
            return CreatedAtAction(nameof(GetList), new { id = entity.Id }, MapDto(created!, stock));
        }
        catch (DomainException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("bulk-add")]
    [RequirePermission(AdminPermissions.OrderList.Create)]
    public async Task<ActionResult<object>> BulkAdd(
        [FromBody] BulkAddSupplierOrderListRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Items is null || request.Items.Count == 0)
        {
            return BadRequest(new { message = "حداقل یک قلم برای افزودن ارسال کنید." });
        }

        try
        {
            var menuIds = request.Items
                .Where(i => i.MenuItemId.HasValue)
                .Select(i => i.MenuItemId!.Value)
                .Distinct()
                .ToList();

            var menuItems = await _dbContext.MenuItems
                .AsNoTracking()
                .Include(m => m.Category)
                .Where(m => menuIds.Contains(m.Id))
                .ToDictionaryAsync(m => m.Id, cancellationToken);

            var stocks = await _dbContext.MenuItemStocks
                .AsNoTracking()
                .Where(s => menuIds.Contains(s.MenuItemId))
                .ToDictionaryAsync(s => s.MenuItemId, cancellationToken);

            var existingPendingByMenuId = await _dbContext.SupplierOrderListItems
                .Where(x => x.Status == SupplierOrderListItemStatus.Pending && x.MenuItemId != null)
                .ToDictionaryAsync(x => x.MenuItemId!.Value, cancellationToken);

            var addedCount = 0;
            var mergedCount = 0;

            foreach (var itemReq in request.Items)
            {
                MenuItem? menuItem = null;
                MenuItemStock? stock = null;
                if (itemReq.MenuItemId.HasValue)
                {
                    menuItems.TryGetValue(itemReq.MenuItemId.Value, out menuItem);
                    stocks.TryGetValue(itemReq.MenuItemId.Value, out stock);
                }

                var qty = itemReq.Quantity > 0 ? itemReq.Quantity : 1;
                var stockSnapshot = stock is not null ? (int)Math.Floor(stock.CurrentQuantity) : (int?)null;

                if (menuItem is not null && existingPendingByMenuId.TryGetValue(menuItem.Id, out var existing))
                {
                    if (request.MergeIfPending)
                    {
                        existing.AddQuantity(qty, stockSnapshot);
                        mergedCount++;
                    }
                    continue;
                }

                var name = !string.IsNullOrWhiteSpace(itemReq.ItemName)
                    ? itemReq.ItemName.Trim()
                    : menuItem?.PersianName ?? string.Empty;

                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                var entity = new SupplierOrderListItem(
                    itemName: name,
                    quantity: qty,
                    unit: string.IsNullOrWhiteSpace(itemReq.Unit) ? "عدد" : itemReq.Unit.Trim(),
                    menuItemId: menuItem?.Id,
                    categoryName: itemReq.CategoryName ?? menuItem?.Category?.Name,
                    supplierId: itemReq.SupplierId ?? request.DefaultSupplierId,
                    supplierNameOverride: itemReq.SupplierNameOverride,
                    supplierPhoneOverride: itemReq.SupplierPhoneOverride,
                    notes: itemReq.Notes,
                    currentStockSnapshot: stockSnapshot);

                _dbContext.SupplierOrderListItems.Add(entity);
                if (menuItem is not null)
                {
                    existingPendingByMenuId[menuItem.Id] = entity;
                }
                addedCount++;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            return Ok(new { addedCount, mergedCount });
        }
        catch (DomainException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(AdminPermissions.OrderList.Update)]
    public async Task<ActionResult<SupplierOrderListItemDto>> UpdateItem(
        Guid id,
        [FromBody] UpdateSupplierOrderListItemRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.SupplierOrderListItems
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
        {
            return NotFound(new { message = "قلم موردنظر در لیست سفارش پیدا نشد." });
        }

        try
        {
            entity.UpdateDetails(
                itemName: request.ItemName ?? entity.ItemName,
                quantity: request.Quantity ?? entity.Quantity,
                unit: request.Unit ?? entity.Unit,
                supplierId: request.ClearSupplier ? null : (request.SupplierId ?? entity.SupplierId),
                supplierNameOverride: request.ClearSupplier ? null : (request.SupplierNameOverride ?? entity.SupplierNameOverride),
                supplierPhoneOverride: request.ClearSupplier ? null : (request.SupplierPhoneOverride ?? entity.SupplierPhoneOverride),
                notes: request.Notes ?? entity.Notes);

            await _dbContext.SaveChangesAsync(cancellationToken);

            var reloaded = await LoadSingleAsync(entity.Id, cancellationToken);
            var stock = reloaded?.MenuItemId is Guid mId
                ? await _dbContext.MenuItemStocks.AsNoTracking().FirstOrDefaultAsync(s => s.MenuItemId == mId, cancellationToken)
                : null;

            return Ok(MapDto(reloaded!, stock));
        }
        catch (DomainException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("mark-ordered")]
    [RequirePermission(AdminPermissions.OrderList.Update)]
    public async Task<ActionResult<object>> MarkOrdered(
        [FromBody] BatchOrderActionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Ids is null || request.Ids.Count == 0)
        {
            return BadRequest(new { message = "هیچ قلمی برای ثبت سفارش انتخاب نشده است." });
        }

        var items = await _dbContext.SupplierOrderListItems
            .Where(x => request.Ids.Contains(x.Id) && x.Status == SupplierOrderListItemStatus.Pending)
            .ToListAsync(cancellationToken);

        if (items.Count == 0)
        {
            return BadRequest(new { message = "قلم در انتظار سفارشی در انتخاب شما پیدا نشد." });
        }

        var nowUtc = DateTime.UtcNow;
        var batchCode = string.IsNullOrWhiteSpace(request.BatchCode)
            ? $"PO-{nowUtc:yyyyMMdd-HHmm}"
            : request.BatchCode.Trim();

        foreach (var item in items)
        {
            item.MarkOrdered(batchCode, nowUtc);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new { updatedCount = items.Count, batchCode });
    }

    [HttpPost("mark-received")]
    [RequirePermission(AdminPermissions.OrderList.Update)]
    public async Task<ActionResult<object>> MarkReceived(
        [FromBody] MarkReceivedRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Ids is null || request.Ids.Count == 0)
        {
            return BadRequest(new { message = "هیچ قلمی برای ثبت تحویل انتخاب نشده است." });
        }

        var items = await _dbContext.SupplierOrderListItems
            .Where(x => request.Ids.Contains(x.Id) &&
                        (x.Status == SupplierOrderListItemStatus.Ordered || x.Status == SupplierOrderListItemStatus.Pending))
            .ToListAsync(cancellationToken);

        if (items.Count == 0)
        {
            return BadRequest(new { message = "قلم قابل تحویلی پیدا نشد." });
        }

        var nowUtc = DateTime.UtcNow;
        var restockedCount = 0;

        Dictionary<Guid, MenuItemStock>? stocksByMenuId = null;
        if (request.RestockMenuItems)
        {
            var menuIds = items
                .Where(i => i.MenuItemId.HasValue)
                .Select(i => i.MenuItemId!.Value)
                .Distinct()
                .ToList();

            stocksByMenuId = await _dbContext.MenuItemStocks
                .Where(s => menuIds.Contains(s.MenuItemId))
                .ToDictionaryAsync(s => s.MenuItemId, cancellationToken);
        }

        foreach (var item in items)
        {
            item.MarkReceived(nowUtc);

            if (request.RestockMenuItems &&
                item.MenuItemId.HasValue &&
                stocksByMenuId is not null &&
                stocksByMenuId.TryGetValue(item.MenuItemId.Value, out var stock))
            {
                stock.AddProduction(item.Quantity);
                restockedCount++;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new { updatedCount = items.Count, restockedCount });
    }

    [HttpPost("{id:guid}/reorder")]
    [RequirePermission(AdminPermissions.OrderList.Create)]
    public async Task<ActionResult<SupplierOrderListItemDto>> Reorder(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var source = await _dbContext.SupplierOrderListItems
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (source is null)
        {
            return NotFound(new { message = "قلم موردنظر پیدا نشد." });
        }

        MenuItemStock? stock = source.MenuItemId.HasValue
            ? await _dbContext.MenuItemStocks.AsNoTracking()
                .FirstOrDefaultAsync(s => s.MenuItemId == source.MenuItemId.Value, cancellationToken)
            : null;

        var cloned = new SupplierOrderListItem(
            itemName: source.ItemName,
            quantity: source.Quantity,
            unit: source.Unit,
            menuItemId: source.MenuItemId,
            categoryName: source.CategoryName,
            supplierId: source.SupplierId,
            supplierNameOverride: source.SupplierNameOverride,
            supplierPhoneOverride: source.SupplierPhoneOverride,
            notes: source.Notes,
            currentStockSnapshot: stock is not null ? (int)Math.Floor(stock.CurrentQuantity) : null);

        _dbContext.SupplierOrderListItems.Add(cloned);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var reloaded = await LoadSingleAsync(cloned.Id, cancellationToken);
        return CreatedAtAction(nameof(GetList), new { id = cloned.Id }, MapDto(reloaded!, stock));
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(AdminPermissions.OrderList.Delete)]
    public async Task<IActionResult> DeleteItem(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.SupplierOrderListItems
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
        {
            return NotFound(new { message = "قلم موردنظر پیدا نشد." });
        }

        _dbContext.SupplierOrderListItems.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpDelete("clear-pending")]
    [RequirePermission(AdminPermissions.OrderList.Delete)]
    public async Task<ActionResult<object>> ClearPending(
        [FromQuery] Guid? supplierId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.SupplierOrderListItems
            .Where(x => x.Status == SupplierOrderListItemStatus.Pending);

        if (supplierId.HasValue)
        {
            query = query.Where(x => x.SupplierId == supplierId.Value);
        }

        var items = await query.ToListAsync(cancellationToken);
        _dbContext.SupplierOrderListItems.RemoveRange(items);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new { removedCount = items.Count });
    }

    private Task<SupplierOrderListItem?> LoadSingleAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.SupplierOrderListItems
            .AsNoTracking()
            .Include(x => x.Supplier)
            .Include(x => x.MenuItem)
                .ThenInclude(m => m!.Category)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    private static SupplierOrderListItemDto MapDto(SupplierOrderListItem x, MenuItemStock? liveStock)
    {
        var supplierName = x.Supplier?.PersianName ?? x.SupplierNameOverride ?? string.Empty;
        var supplierPhone = !string.IsNullOrWhiteSpace(x.SupplierPhoneOverride)
            ? x.SupplierPhoneOverride
            : (x.Supplier?.PhoneNumber ?? string.Empty);

        return new SupplierOrderListItemDto(
            Id: x.Id,
            MenuItemId: x.MenuItemId,
            ItemName: x.ItemName,
            CategoryName: x.CategoryName ?? x.MenuItem?.Category?.Name ?? string.Empty,
            SupplierId: x.SupplierId,
            SupplierName: supplierName,
            SupplierPhone: supplierPhone,
            SupplierContactName: x.Supplier?.ContactName ?? string.Empty,
            Quantity: x.Quantity,
            Unit: x.Unit,
            Notes: x.Notes ?? string.Empty,
            Status: x.Status.ToString(),
            BatchCode: x.BatchCode,
            OrderedAt: x.OrderedAt,
            ReceivedAt: x.ReceivedAt,
            CreatedAt: x.CreatedAt,
            CurrentStock: liveStock?.CurrentQuantity,
            MinStockAlert: liveStock?.MinAlertQuantity,
            CurrentStockSnapshot: x.CurrentStockSnapshot);
    }
}

public record SupplierOrderListResponseDto(
    int PendingCount,
    int OrderedCount,
    int ReceivedCount,
    IReadOnlyList<SupplierOrderListItemDto> Items);

public record SupplierOrderListItemDto(
    Guid Id,
    Guid? MenuItemId,
    string ItemName,
    string CategoryName,
    Guid? SupplierId,
    string SupplierName,
    string SupplierPhone,
    string SupplierContactName,
    decimal Quantity,
    string Unit,
    string Notes,
    string Status,
    string? BatchCode,
    DateTime? OrderedAt,
    DateTime? ReceivedAt,
    DateTimeOffset CreatedAt,
    decimal? CurrentStock,
    decimal? MinStockAlert,
    int? CurrentStockSnapshot);

public record LowStockSuggestionDto(
    Guid MenuItemId,
    string ItemName,
    string CategoryName,
    decimal CurrentQuantity,
    decimal MinAlertQuantity,
    decimal SuggestedOrderQuantity,
    bool IsOutOfStock,
    bool AlreadyInOrderList);

public record AddSupplierOrderListItemRequest(
    Guid? MenuItemId = null,
    string? ItemName = null,
    string? CategoryName = null,
    Guid? SupplierId = null,
    string? SupplierNameOverride = null,
    string? SupplierPhoneOverride = null,
    decimal Quantity = 1,
    string? Unit = "عدد",
    string? Notes = null,
    bool MergeIfPending = true);

public record BulkAddSupplierOrderListRequest(
    IReadOnlyList<AddSupplierOrderListItemRequest> Items,
    Guid? DefaultSupplierId = null,
    bool MergeIfPending = true);

public record UpdateSupplierOrderListItemRequest(
    string? ItemName = null,
    decimal? Quantity = null,
    string? Unit = null,
    Guid? SupplierId = null,
    string? SupplierNameOverride = null,
    string? SupplierPhoneOverride = null,
    string? Notes = null,
    bool ClearSupplier = false);

public record BatchOrderActionRequest(
    IReadOnlyList<Guid> Ids,
    string? BatchCode = null);

public record MarkReceivedRequest(
    IReadOnlyList<Guid> Ids,
    bool RestockMenuItems = true);
