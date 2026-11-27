using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sugarista.Api.Authorization;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.Recipes.Commands.DeleteRecipeItem;
using Sugarista.Application.Recipes.Commands.UpdateMenuItemRecipeDescription;
using Sugarista.Application.Recipes.Commands.UpsertRecipeItem;
using Sugarista.Application.Recipes.Queries.GetMenuItemRecipe;
using Sugarista.Domain.Constants;
using Sugarista.Domain.Entities;

namespace Sugarista.Api.Controllers;

[Authorize]
[Route("api/admin/recipes")]
public class AdminRecipesController : ApiControllerBase
{
    private readonly IApplicationDbContext _dbContext;

    public AdminRecipesController(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("cards")]
    [RequirePermission(AdminPermissions.Recipes.View)]
    public async Task<ActionResult<RecipeCardsGalleryResponseDto>> GetCards(CancellationToken cancellationToken)
    {
        var items = await _dbContext.MenuItems
            .AsNoTracking()
            .Include(m => m.Category)
            .OrderBy(m => m.Category.DisplayOrder)
            .ThenBy(m => m.DisplayOrder)
            .ThenBy(m => m.PersianName)
            .Select(m => new
            {
                m.Id,
                m.PersianName,
                m.EnglishName,
                m.ImageUrl,
                m.RecipeDescription,
                m.IsActive,
                m.CategoryId,
                CategoryName = m.Category.Name,
                CategoryDisplayOrder = m.Category.DisplayOrder,
            })
            .ToListAsync(cancellationToken);

        var cards = await _dbContext.MenuItemRecipeCards
            .AsNoTracking()
            .ToDictionaryAsync(c => c.MenuItemId, cancellationToken);

        var allLines = await _dbContext.RecipeItems
            .AsNoTracking()
            .Include(r => r.Ingredient)
            .OrderBy(r => r.Ingredient.PersianName)
            .Select(r => new
            {
                r.MenuItemId,
                Dto = new RecipeCardIngredientDto(
                    r.Id,
                    r.IngredientId,
                    r.Ingredient.PersianName,
                    r.Ingredient.Unit,
                    r.Quantity,
                    r.ShowToCustomer)
            })
            .ToListAsync(cancellationToken);

        var linesByItem = allLines
            .GroupBy(x => x.MenuItemId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<RecipeCardIngredientDto>)g.Select(x => x.Dto).ToList());

        var categories = items
            .GroupBy(x => new { x.CategoryId, x.CategoryName, x.CategoryDisplayOrder })
            .OrderBy(g => g.Key.CategoryDisplayOrder)
            .ThenBy(g => g.Key.CategoryName)
            .Select(g => new RecipeCardCategoryDto(g.Key.CategoryId, g.Key.CategoryName, g.Count()))
            .ToList();

        var resultCards = items.Select(item =>
        {
            cards.TryGetValue(item.Id, out var card);
            linesByItem.TryGetValue(item.Id, out var itemLines);
            var bullets = card?.GetBulletPoints() ?? [];
            var hasCard = card is not null && (
                !string.IsNullOrWhiteSpace(card.CardImageUrl) ||
                bullets.Count > 0 ||
                !string.IsNullOrWhiteSpace(card.BaristaTip) ||
                !string.IsNullOrWhiteSpace(card.ServingTemp) ||
                !string.IsNullOrWhiteSpace(card.GlasswareOrPlate) ||
                card.PrepSeconds.HasValue);

            return new RecipeCardItemDto(
                MenuItemId: item.Id,
                PersianName: item.PersianName,
                EnglishName: item.EnglishName,
                CategoryId: item.CategoryId,
                CategoryName: item.CategoryName,
                IsActive: item.IsActive,
                ProductImageUrl: item.ImageUrl,
                CardImageUrl: card?.CardImageUrl,
                EffectiveImageUrl: !string.IsNullOrWhiteSpace(card?.CardImageUrl) ? card.CardImageUrl : item.ImageUrl,
                RecipeDescription: item.RecipeDescription,
                BulletPoints: bullets,
                ServingTemp: card?.ServingTemp,
                GlasswareOrPlate: card?.GlasswareOrPlate,
                PrepSeconds: card?.PrepSeconds,
                BaristaTip: card?.BaristaTip,
                HasCustomCard: hasCard,
                Ingredients: itemLines ?? []);
        }).ToList();

        return Ok(new RecipeCardsGalleryResponseDto(categories, resultCards));
    }

    [HttpPut("{menuItemId:guid}/card")]
    [RequirePermission(AdminPermissions.Recipes.Update)]
    public async Task<ActionResult<RecipeCardItemDto>> UpsertCard(
        Guid menuItemId,
        [FromBody] UpsertRecipeCardRequest request,
        CancellationToken cancellationToken)
    {
        var menuItem = await _dbContext.MenuItems
            .Include(m => m.Category)
            .FirstOrDefaultAsync(m => m.Id == menuItemId, cancellationToken);
        if (menuItem is null)
        {
            return NotFound(new { detail = "آیتم منو یافت نشد." });
        }

        var card = await _dbContext.MenuItemRecipeCards
            .FirstOrDefaultAsync(c => c.MenuItemId == menuItemId, cancellationToken);

        if (card is null)
        {
            card = new MenuItemRecipeCard(
                menuItemId,
                request.CardImageUrl,
                request.BulletPoints,
                request.ServingTemp,
                request.GlasswareOrPlate,
                request.PrepSeconds,
                request.BaristaTip);
            _dbContext.MenuItemRecipeCards.Add(card);
        }
        else
        {
            card.Update(
                request.CardImageUrl,
                request.BulletPoints,
                request.ServingTemp,
                request.GlasswareOrPlate,
                request.PrepSeconds,
                request.BaristaTip);
        }

        // Only update RecipeDescription when explicitly sent, never touching RecipeItems!
        if (request.UpdateDescription)
        {
            menuItem.UpdateRecipeDescription(request.RecipeDescription);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var ingredients = await _dbContext.RecipeItems
            .AsNoTracking()
            .Where(r => r.MenuItemId == menuItemId)
            .OrderBy(r => r.Ingredient.PersianName)
            .Select(r => new RecipeCardIngredientDto(
                r.Id,
                r.IngredientId,
                r.Ingredient.PersianName,
                r.Ingredient.Unit,
                r.Quantity,
                r.ShowToCustomer))
            .ToListAsync(cancellationToken);

        var bullets = card.GetBulletPoints();
        return Ok(new RecipeCardItemDto(
            MenuItemId: menuItem.Id,
            PersianName: menuItem.PersianName,
            EnglishName: menuItem.EnglishName,
            CategoryId: menuItem.CategoryId,
            CategoryName: menuItem.Category?.Name ?? string.Empty,
            IsActive: menuItem.IsActive,
            ProductImageUrl: menuItem.ImageUrl,
            CardImageUrl: card.CardImageUrl,
            EffectiveImageUrl: !string.IsNullOrWhiteSpace(card.CardImageUrl) ? card.CardImageUrl : menuItem.ImageUrl,
            RecipeDescription: menuItem.RecipeDescription,
            BulletPoints: bullets,
            ServingTemp: card.ServingTemp,
            GlasswareOrPlate: card.GlasswareOrPlate,
            PrepSeconds: card.PrepSeconds,
            BaristaTip: card.BaristaTip,
            HasCustomCard: true,
            Ingredients: ingredients));
    }

    [HttpGet("{menuItemId:guid}")]
    [RequirePermission(AdminPermissions.Recipes.View)]
    public async Task<ActionResult<MenuItemRecipeDto>> GetByMenuItem(Guid menuItemId, CancellationToken cancellationToken)
    {
        return Ok(await Mediator.Send(new GetMenuItemRecipeQuery(menuItemId), cancellationToken));
    }

    [HttpPut("{menuItemId:guid}/description")]
    [RequirePermission(AdminPermissions.Recipes.Update)]
    public async Task<IActionResult> UpdateDescription(
        Guid menuItemId,
        [FromBody] UpdateMenuItemRecipeDescriptionRequest request,
        CancellationToken cancellationToken)
    {
        await Mediator.Send(new UpdateMenuItemRecipeDescriptionCommand(menuItemId, request.Description), cancellationToken);
        return NoContent();
    }

    [HttpPost("{menuItemId:guid}")]
    [RequirePermission(AdminPermissions.Recipes.Create)]
    public async Task<ActionResult<Guid>> Upsert(Guid menuItemId, [FromBody] UpsertRecipeItemCommand command, CancellationToken cancellationToken)
    {
        var id = await Mediator.Send(command with { MenuItemId = menuItemId }, cancellationToken);
        return Ok(id);
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(AdminPermissions.Recipes.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await Mediator.Send(new DeleteRecipeItemCommand(id), cancellationToken);
        return NoContent();
    }
}

public record UpdateMenuItemRecipeDescriptionRequest(string? Description);

public record RecipeCardCategoryDto(Guid Id, string Name, int ItemCount);

public record RecipeCardIngredientDto(
    Guid Id,
    Guid IngredientId,
    string IngredientName,
    string Unit,
    decimal Quantity,
    bool ShowToCustomer);

public record RecipeCardItemDto(
    Guid MenuItemId,
    string PersianName,
    string EnglishName,
    Guid CategoryId,
    string CategoryName,
    bool IsActive,
    string? ProductImageUrl,
    string? CardImageUrl,
    string? EffectiveImageUrl,
    string? RecipeDescription,
    IReadOnlyList<string> BulletPoints,
    string? ServingTemp,
    string? GlasswareOrPlate,
    int? PrepSeconds,
    string? BaristaTip,
    bool HasCustomCard,
    IReadOnlyList<RecipeCardIngredientDto> Ingredients);

public record RecipeCardsGalleryResponseDto(
    IReadOnlyList<RecipeCardCategoryDto> Categories,
    IReadOnlyList<RecipeCardItemDto> Cards);

public record UpsertRecipeCardRequest(
    string? CardImageUrl,
    IReadOnlyList<string>? BulletPoints,
    string? ServingTemp,
    string? GlasswareOrPlate,
    int? PrepSeconds,
    string? BaristaTip,
    string? RecipeDescription = null,
    bool UpdateDescription = false);
