using System.Security.Claims;
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
/// Tracks and aggregates unmet customer demands (items/options customers ask for that the cafe
/// does not carry yet) across any date range, producing ranked demand summaries for menu decisions.
/// </summary>
[Authorize]
[Route("api/admin/customer-demands")]
public class AdminCustomerDemandsController : ApiControllerBase
{
    private readonly IApplicationDbContext _dbContext;

    public AdminCustomerDemandsController(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    [RequirePermission(AdminPermissions.CustomerDemands.View)]
    public async Task<ActionResult<CustomerDemandBoardDto>> GetBoard(
        [FromQuery] DateTimeOffset? fromDate = null,
        [FromQuery] DateTimeOffset? toDate = null,
        [FromQuery] CustomerDemandCategory? category = null,
        [FromQuery] CustomerDemandStatus? status = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.CustomerDemands.AsNoTracking().AsQueryable();

        if (fromDate.HasValue)
        {
            query = query.Where(x => x.RequestedAt >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(x => x.RequestedAt <= toDate.Value);
        }

        if (category.HasValue)
        {
            query = query.Where(x => x.Category == category.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        var rows = await query
            .OrderByDescending(x => x.RequestedAt)
            .ThenByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        IEnumerable<CustomerDemand> filtered = rows;
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            var normalizedTerm = CustomerDemand.NormalizeDemandKey(term);
            filtered = filtered.Where(x =>
                x.RequestedItem.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                x.NormalizedKey.Contains(normalizedTerm, StringComparison.OrdinalIgnoreCase) ||
                (x.CustomerName != null && x.CustomerName.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                (x.CustomerPhone != null && x.CustomerPhone.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                (x.Notes != null && x.Notes.Contains(term, StringComparison.OrdinalIgnoreCase)));
        }

        var list = filtered.ToList();
        var totalRequests = list.Sum(x => x.RequestedCount);

        var ranked = list
            .GroupBy(x => x.NormalizedKey)
            .Select(g =>
            {
                var latest = g.OrderByDescending(i => i.RequestedAt).First();
                var groupTotal = g.Sum(i => i.RequestedCount);
                var uniquePhones = g
                    .Where(i => !string.IsNullOrWhiteSpace(i.CustomerPhone))
                    .Select(i => i.CustomerPhone!.Trim())
                    .Distinct()
                    .Count();
                var pct = totalRequests > 0
                    ? Math.Round((double)groupTotal / totalRequests * 100.0, 1)
                    : 0d;

                return new CustomerDemandRankedItemDto(
                    NormalizedKey: g.Key,
                    ItemName: latest.RequestedItem,
                    Category: latest.Category.ToString(),
                    Status: latest.Status.ToString(),
                    TotalRequests: groupTotal,
                    EntryCount: g.Count(),
                    UniqueCustomerPhones: uniquePhones,
                    LastRequestedAt: latest.RequestedAt,
                    PercentOfTotal: pct);
            })
            .OrderByDescending(x => x.TotalRequests)
            .ThenByDescending(x => x.LastRequestedAt)
            .ToList();

        var addedToMenuCount = ranked.Count(r => r.Status == CustomerDemandStatus.AddedToMenu.ToString());
        var plannedCount = ranked.Count(r => r.Status == CustomerDemandStatus.Planned.ToString());

        return Ok(new CustomerDemandBoardDto(
            TotalRequests: totalRequests,
            UniqueItemsCount: ranked.Count,
            TopRequestedItem: ranked.FirstOrDefault()?.ItemName ?? string.Empty,
            TopRequestedCount: ranked.FirstOrDefault()?.TotalRequests ?? 0,
            PlannedItemsCount: plannedCount,
            AddedToMenuCount: addedToMenuCount,
            RankedSummary: ranked,
            Entries: list.Select(MapDto).ToList()));
    }

    [HttpPost]
    [RequireAnyPermission(AdminPermissions.CustomerDemands.Create, AdminPermissions.Pos.View)]
    public async Task<ActionResult<CustomerDemandDto>> Create(
        [FromBody] CreateCustomerDemandRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var recorder = ResolveCurrentAdminName();
            var entity = new CustomerDemand(
                requestedItem: request.RequestedItem ?? string.Empty,
                category: request.Category,
                requestedCount: request.RequestedCount > 0 ? request.RequestedCount : 1,
                requestedAt: request.RequestedAt ?? DateTimeOffset.UtcNow,
                channel: request.Channel ?? "POS",
                status: request.Status,
                customerName: request.CustomerName,
                customerPhone: request.CustomerPhone,
                notes: request.Notes,
                recordedByName: recorder);

            _dbContext.CustomerDemands.Add(entity);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return CreatedAtAction(nameof(GetBoard), new { id = entity.Id }, MapDto(entity));
        }
        catch (DomainException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("update-group-status")]
    [RequirePermission(AdminPermissions.CustomerDemands.Update)]
    public async Task<ActionResult<object>> UpdateGroupStatus(
        [FromBody] UpdateDemandGroupStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.NormalizedKey))
        {
            return BadRequest(new { message = "کلید آیتم نامعتبر است." });
        }

        var key = CustomerDemand.NormalizeDemandKey(request.NormalizedKey);
        var rows = await _dbContext.CustomerDemands
            .Where(x => x.NormalizedKey == key)
            .ToListAsync(cancellationToken);

        foreach (var row in rows)
        {
            row.SetStatus(request.Status);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Ok(new { updatedCount = rows.Count });
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(AdminPermissions.CustomerDemands.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.CustomerDemands
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
        {
            return NotFound(new { message = "درخواست موردنظر پیدا نشد." });
        }

        _dbContext.CustomerDemands.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private string? ResolveCurrentAdminName()
    {
        return User.FindFirstValue("display_name")
            ?? User.FindFirstValue(ClaimTypes.Name)
            ?? User.Identity?.Name;
    }

    private static CustomerDemandDto MapDto(CustomerDemand x) => new(
        Id: x.Id,
        RequestedItem: x.RequestedItem,
        NormalizedKey: x.NormalizedKey,
        Category: x.Category.ToString(),
        Status: x.Status.ToString(),
        RequestedCount: x.RequestedCount,
        RequestedAt: x.RequestedAt,
        Channel: x.Channel,
        CustomerName: x.CustomerName ?? string.Empty,
        CustomerPhone: x.CustomerPhone ?? string.Empty,
        Notes: x.Notes ?? string.Empty,
        RecordedByName: x.RecordedByName ?? string.Empty);
}

public record CustomerDemandBoardDto(
    int TotalRequests,
    int UniqueItemsCount,
    string TopRequestedItem,
    int TopRequestedCount,
    int PlannedItemsCount,
    int AddedToMenuCount,
    IReadOnlyList<CustomerDemandRankedItemDto> RankedSummary,
    IReadOnlyList<CustomerDemandDto> Entries);

public record CustomerDemandRankedItemDto(
    string NormalizedKey,
    string ItemName,
    string Category,
    string Status,
    int TotalRequests,
    int EntryCount,
    int UniqueCustomerPhones,
    DateTimeOffset LastRequestedAt,
    double PercentOfTotal);

public record CustomerDemandDto(
    Guid Id,
    string RequestedItem,
    string NormalizedKey,
    string Category,
    string Status,
    int RequestedCount,
    DateTimeOffset RequestedAt,
    string Channel,
    string CustomerName,
    string CustomerPhone,
    string Notes,
    string RecordedByName);

public record CreateCustomerDemandRequest(
    string? RequestedItem,
    CustomerDemandCategory Category = CustomerDemandCategory.Other,
    int RequestedCount = 1,
    DateTimeOffset? RequestedAt = null,
    string? Channel = "POS",
    CustomerDemandStatus Status = CustomerDemandStatus.Open,
    string? CustomerName = null,
    string? CustomerPhone = null,
    string? Notes = null);

public record UpdateDemandGroupStatusRequest(
    string NormalizedKey,
    CustomerDemandStatus Status);
