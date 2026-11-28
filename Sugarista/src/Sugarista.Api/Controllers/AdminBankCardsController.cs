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
/// Manages cafe/admin bank cards used for Card-to-Card (کارت به کارت) payments in POS.
/// Readable by both POS cashiers and SiteSettings admins; editable by SiteSettings admins.
/// </summary>
[Authorize]
[Route("api/admin/bank-cards")]
public class AdminBankCardsController : ApiControllerBase
{
    private readonly IApplicationDbContext _dbContext;

    public AdminBankCardsController(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    [RequireAnyPermission(AdminPermissions.SiteSettings.View, AdminPermissions.Pos.View)]
    public async Task<ActionResult<IReadOnlyList<AdminBankCardDto>>> GetAll(
        [FromQuery] bool activeOnly = false,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.AdminBankCards.AsNoTracking().AsQueryable();
        if (activeOnly)
        {
            query = query.Where(c => c.IsActive);
        }

        var cards = await query
            .OrderByDescending(c => c.IsDefault)
            .ThenBy(c => c.DisplayOrder)
            .ThenByDescending(c => c.CreatedAt)
            .ToListAsync(cancellationToken);

        return Ok(cards.Select(MapDto).ToList());
    }

    [HttpPost]
    [RequirePermission(AdminPermissions.SiteSettings.Update)]
    public async Task<ActionResult<AdminBankCardDto>> Create(
        [FromBody] UpsertAdminBankCardRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var hasAny = await _dbContext.AdminBankCards.AnyAsync(cancellationToken);
            var shouldBeDefault = request.IsDefault || !hasAny;

            if (shouldBeDefault)
            {
                await ClearDefaultFlagsAsync(excludeId: null, cancellationToken);
            }

            var entity = new AdminBankCard(
                title: request.Title ?? string.Empty,
                bankName: request.BankName ?? string.Empty,
                cardHolderName: request.CardHolderName ?? string.Empty,
                cardNumber: request.CardNumber ?? string.Empty,
                shebaNumber: request.ShebaNumber,
                cardImageUrl: request.CardImageUrl,
                cardTheme: request.CardTheme ?? "ocean",
                isDefault: shouldBeDefault,
                isActive: request.IsActive,
                displayOrder: request.DisplayOrder);

            _dbContext.AdminBankCards.Add(entity);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return CreatedAtAction(nameof(GetAll), new { id = entity.Id }, MapDto(entity));
        }
        catch (DomainException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(AdminPermissions.SiteSettings.Update)]
    public async Task<ActionResult<AdminBankCardDto>> Update(
        Guid id,
        [FromBody] UpsertAdminBankCardRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.AdminBankCards
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (entity is null)
        {
            return NotFound(new { message = "کارت بانکی موردنظر پیدا نشد." });
        }

        try
        {
            if (request.IsDefault)
            {
                await ClearDefaultFlagsAsync(excludeId: id, cancellationToken);
            }

            entity.Update(
                title: request.Title ?? string.Empty,
                bankName: request.BankName ?? string.Empty,
                cardHolderName: request.CardHolderName ?? string.Empty,
                cardNumber: request.CardNumber ?? string.Empty,
                shebaNumber: request.ShebaNumber,
                cardImageUrl: request.CardImageUrl,
                cardTheme: request.CardTheme ?? "ocean",
                isDefault: request.IsDefault,
                isActive: request.IsActive,
                displayOrder: request.DisplayOrder);

            await _dbContext.SaveChangesAsync(cancellationToken);
            return Ok(MapDto(entity));
        }
        catch (DomainException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(AdminPermissions.SiteSettings.Update)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.AdminBankCards
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (entity is null)
        {
            return NotFound(new { message = "کارت بانکی موردنظر پیدا نشد." });
        }

        var wasDefault = entity.IsDefault;
        _dbContext.AdminBankCards.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (wasDefault)
        {
            var next = await _dbContext.AdminBankCards
                .OrderBy(c => c.DisplayOrder)
                .FirstOrDefaultAsync(cancellationToken);
            if (next is not null)
            {
                next.SetDefault(true);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        return NoContent();
    }

    private async Task ClearDefaultFlagsAsync(Guid? excludeId, CancellationToken cancellationToken)
    {
        var defaults = await _dbContext.AdminBankCards
            .Where(c => c.IsDefault && (!excludeId.HasValue || c.Id != excludeId.Value))
            .ToListAsync(cancellationToken);

        foreach (var card in defaults)
        {
            card.SetDefault(false);
        }
    }

    private static AdminBankCardDto MapDto(AdminBankCard c) => new(
        Id: c.Id,
        Title: c.Title,
        BankName: c.BankName,
        CardHolderName: c.CardHolderName,
        CardNumber: c.CardNumber,
        ShebaNumber: c.ShebaNumber ?? string.Empty,
        CardImageUrl: c.CardImageUrl ?? string.Empty,
        CardTheme: c.CardTheme,
        IsDefault: c.IsDefault,
        IsActive: c.IsActive,
        DisplayOrder: c.DisplayOrder);
}

public record AdminBankCardDto(
    Guid Id,
    string Title,
    string BankName,
    string CardHolderName,
    string CardNumber,
    string ShebaNumber,
    string CardImageUrl,
    string CardTheme,
    bool IsDefault,
    bool IsActive,
    int DisplayOrder);

public record UpsertAdminBankCardRequest(
    string? Title,
    string? BankName,
    string? CardHolderName,
    string? CardNumber,
    string? ShebaNumber = null,
    string? CardImageUrl = null,
    string? CardTheme = "ocean",
    bool IsDefault = false,
    bool IsActive = true,
    int DisplayOrder = 0);
