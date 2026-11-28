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
/// Cafe Operational Incident Log (ثبت و مدیریت حوادث):
/// records power/water/internet outages, equipment breakdowns, safety & customer incidents
/// with exact occurrence & resolution timestamps, downtime tracking, and financial impact.
/// </summary>
[Authorize]
[Route("api/admin/incidents")]
public class AdminIncidentsController : ApiControllerBase
{
    private readonly IApplicationDbContext _dbContext;

    public AdminIncidentsController(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    [RequirePermission(AdminPermissions.Incidents.View)]
    public async Task<ActionResult<IncidentBoardResponseDto>> GetAll(
        [FromQuery] IncidentStatus? status = null,
        [FromQuery] IncidentCategory? category = null,
        [FromQuery] IncidentSeverity? severity = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var all = await _dbContext.CafeIncidents
            .AsNoTracking()
            .OrderByDescending(x => x.OccurredAt)
            .ThenByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        var thirtyDaysAgo = DateTimeOffset.UtcNow.AddDays(-30);
        var openCount = all.Count(x => x.Status is IncidentStatus.Open or IncidentStatus.Investigating);
        var criticalOpenCount = all.Count(x =>
            x.Status is IncidentStatus.Open or IncidentStatus.Investigating &&
            x.Severity == IncidentSeverity.Critical);
        var last30DaysCount = all.Count(x => x.OccurredAt >= thirtyDaysAgo);
        var totalDowntimeMinutes = all.Sum(x => x.DurationMinutes ?? 0);
        var totalEstimatedLoss = all.Sum(x => x.EstimatedLossAmount ?? 0m);

        IEnumerable<CafeIncident> filtered = all;

        if (status.HasValue)
        {
            filtered = filtered.Where(x => x.Status == status.Value);
        }

        if (category.HasValue)
        {
            filtered = filtered.Where(x => x.Category == category.Value);
        }

        if (severity.HasValue)
        {
            filtered = filtered.Where(x => x.Severity == severity.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            filtered = filtered.Where(x =>
                x.Title.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                x.IncidentCode.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (x.Location != null && x.Location.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                (x.Description != null && x.Description.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                (x.ImpactSummary != null && x.ImpactSummary.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                (x.ResolutionNotes != null && x.ResolutionNotes.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                (x.ReportedByName != null && x.ReportedByName.Contains(term, StringComparison.OrdinalIgnoreCase)));
        }

        var items = filtered.Select(MapDto).ToList();

        return Ok(new IncidentBoardResponseDto(
            TotalCount: all.Count,
            OpenCount: openCount,
            CriticalOpenCount: criticalOpenCount,
            Last30DaysCount: last30DaysCount,
            TotalDowntimeMinutes: totalDowntimeMinutes,
            TotalEstimatedLoss: totalEstimatedLoss,
            Items: items));
    }

    [HttpPost]
    [RequirePermission(AdminPermissions.Incidents.Create)]
    public async Task<ActionResult<CafeIncidentDto>> Create(
        [FromBody] UpsertCafeIncidentRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var nowUtc = DateTimeOffset.UtcNow;
            var occurredAt = request.OccurredAt ?? nowUtc;
            var code = await GenerateUniqueCodeAsync(occurredAt, cancellationToken);
            var reporter = !string.IsNullOrWhiteSpace(request.ReportedByName)
                ? request.ReportedByName.Trim()
                : ResolveCurrentAdminName();

            var resolvedStatus = request.ResolvedAt.HasValue && request.Status == IncidentStatus.Open
                ? IncidentStatus.Resolved
                : request.Status;

            var entity = new CafeIncident(
                incidentCode: code,
                title: request.Title ?? string.Empty,
                category: request.Category,
                severity: request.Severity,
                occurredAt: occurredAt,
                resolvedAt: request.ResolvedAt,
                status: resolvedStatus,
                location: request.Location,
                description: request.Description,
                impactSummary: request.ImpactSummary,
                estimatedLossAmount: request.EstimatedLossAmount,
                resolutionNotes: request.ResolutionNotes,
                reportedByName: reporter,
                resolvedByName: request.ResolvedByName ?? (request.ResolvedAt.HasValue ? reporter : null),
                photoUrl: request.PhotoUrl);

            _dbContext.CafeIncidents.Add(entity);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return CreatedAtAction(nameof(GetAll), new { id = entity.Id }, MapDto(entity));
        }
        catch (DomainException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(AdminPermissions.Incidents.Update)]
    public async Task<ActionResult<CafeIncidentDto>> Update(
        Guid id,
        [FromBody] UpsertCafeIncidentRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.CafeIncidents
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
        {
            return NotFound(new { message = "حادثه موردنظر پیدا نشد." });
        }

        try
        {
            entity.Update(
                title: request.Title ?? entity.Title,
                category: request.Category,
                severity: request.Severity,
                status: request.Status,
                occurredAt: request.OccurredAt ?? entity.OccurredAt,
                resolvedAt: request.ResolvedAt,
                location: request.Location,
                description: request.Description,
                impactSummary: request.ImpactSummary,
                estimatedLossAmount: request.EstimatedLossAmount,
                resolutionNotes: request.ResolutionNotes,
                reportedByName: request.ReportedByName ?? entity.ReportedByName,
                resolvedByName: request.ResolvedByName ?? entity.ResolvedByName,
                photoUrl: request.PhotoUrl);

            await _dbContext.SaveChangesAsync(cancellationToken);
            return Ok(MapDto(entity));
        }
        catch (DomainException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/resolve")]
    [RequirePermission(AdminPermissions.Incidents.Update)]
    public async Task<ActionResult<CafeIncidentDto>> Resolve(
        Guid id,
        [FromBody] ResolveCafeIncidentRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.CafeIncidents
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
        {
            return NotFound(new { message = "حادثه موردنظر پیدا نشد." });
        }

        try
        {
            var resolvedAt = request.ResolvedAt ?? DateTimeOffset.UtcNow;
            var resolver = !string.IsNullOrWhiteSpace(request.ResolvedByName)
                ? request.ResolvedByName.Trim()
                : ResolveCurrentAdminName();

            entity.MarkResolved(resolvedAt, request.ResolutionNotes, resolver);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return Ok(MapDto(entity));
        }
        catch (DomainException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(AdminPermissions.Incidents.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.CafeIncidents
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
        {
            return NotFound(new { message = "حادثه موردنظر پیدا نشد." });
        }

        _dbContext.CafeIncidents.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private async Task<string> GenerateUniqueCodeAsync(DateTimeOffset occurredAt, CancellationToken cancellationToken)
    {
        var prefix = $"INC-{occurredAt.UtcDateTime:yyyyMMdd}-";
        var countToday = await _dbContext.CafeIncidents
            .AsNoTracking()
            .CountAsync(x => x.IncidentCode.StartsWith(prefix), cancellationToken);

        for (var seq = countToday + 1; seq < countToday + 100; seq++)
        {
            var candidate = $"{prefix}{seq:D2}";
            var exists = await _dbContext.CafeIncidents
                .AsNoTracking()
                .AnyAsync(x => x.IncidentCode == candidate, cancellationToken);
            if (!exists)
            {
                return candidate;
            }
        }

        return $"{prefix}{Guid.NewGuid().ToString("N")[..4].ToUpperInvariant()}";
    }

    private string? ResolveCurrentAdminName()
    {
        return User.FindFirstValue("display_name")
            ?? User.FindFirstValue(ClaimTypes.Name)
            ?? User.Identity?.Name;
    }

    private static CafeIncidentDto MapDto(CafeIncident x) => new(
        Id: x.Id,
        IncidentCode: x.IncidentCode,
        Title: x.Title,
        Category: x.Category.ToString(),
        Severity: x.Severity.ToString(),
        Status: x.Status.ToString(),
        OccurredAt: x.OccurredAt,
        ResolvedAt: x.ResolvedAt,
        DurationMinutes: x.DurationMinutes,
        Location: x.Location ?? string.Empty,
        Description: x.Description ?? string.Empty,
        ImpactSummary: x.ImpactSummary ?? string.Empty,
        EstimatedLossAmount: x.EstimatedLossAmount,
        ResolutionNotes: x.ResolutionNotes ?? string.Empty,
        ReportedByName: x.ReportedByName ?? string.Empty,
        ResolvedByName: x.ResolvedByName ?? string.Empty,
        PhotoUrl: x.PhotoUrl ?? string.Empty,
        CreatedAt: x.CreatedAt);
}

public record IncidentBoardResponseDto(
    int TotalCount,
    int OpenCount,
    int CriticalOpenCount,
    int Last30DaysCount,
    int TotalDowntimeMinutes,
    decimal TotalEstimatedLoss,
    IReadOnlyList<CafeIncidentDto> Items);

public record CafeIncidentDto(
    Guid Id,
    string IncidentCode,
    string Title,
    string Category,
    string Severity,
    string Status,
    DateTimeOffset OccurredAt,
    DateTimeOffset? ResolvedAt,
    int? DurationMinutes,
    string Location,
    string Description,
    string ImpactSummary,
    decimal? EstimatedLossAmount,
    string ResolutionNotes,
    string ReportedByName,
    string ResolvedByName,
    string PhotoUrl,
    DateTimeOffset CreatedAt);

public record UpsertCafeIncidentRequest(
    string? Title,
    IncidentCategory Category = IncidentCategory.Other,
    IncidentSeverity Severity = IncidentSeverity.Medium,
    IncidentStatus Status = IncidentStatus.Open,
    DateTimeOffset? OccurredAt = null,
    DateTimeOffset? ResolvedAt = null,
    string? Location = null,
    string? Description = null,
    string? ImpactSummary = null,
    decimal? EstimatedLossAmount = null,
    string? ResolutionNotes = null,
    string? ReportedByName = null,
    string? ResolvedByName = null,
    string? PhotoUrl = null);

public record ResolveCafeIncidentRequest(
    DateTimeOffset? ResolvedAt = null,
    string? ResolutionNotes = null,
    string? ResolvedByName = null);
