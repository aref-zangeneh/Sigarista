using Microsoft.EntityFrameworkCore;

namespace Sugarista.Application.Recipes.Queries.GetMenuItemRecipe;

public record GetMenuItemRecipeQuery(Guid MenuItemId) : IRequest<MenuItemRecipeDto>;

public record MenuItemRecipeDto(
    Guid MenuItemId,
    string MenuItemPersianName,
    string MenuItemEnglishName,
    string? Description,
    IReadOnlyCollection<RecipeItemDto> Lines);

public record RecipeItemDto(
    Guid Id,
    Guid IngredientId,
    string IngredientPersianName,
    string IngredientEnglishName,
    string Unit,
    decimal Quantity,
    string? PreparationNote);

public class GetMenuItemRecipeQueryHandler : IRequestHandler<GetMenuItemRecipeQuery, MenuItemRecipeDto>
{
    private readonly IApplicationDbContext _dbContext;

    public GetMenuItemRecipeQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<MenuItemRecipeDto> Handle(GetMenuItemRecipeQuery request, CancellationToken cancellationToken)
    {
        var menuItem = await _dbContext.MenuItems
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == request.MenuItemId, cancellationToken)
            ?? throw new KeyNotFoundException("Menu item was not found.");

        var lines = await _dbContext.RecipeItems
            .AsNoTracking()
            .Where(r => r.MenuItemId == request.MenuItemId)
            .OrderBy(r => r.Ingredient.PersianName)
            .Select(r => new RecipeItemDto(
                r.Id,
                r.IngredientId,
                r.Ingredient.PersianName,
                r.Ingredient.EnglishName,
                r.Ingredient.Unit,
                r.Quantity,
                r.PreparationNote))
            .ToListAsync(cancellationToken);

        return new MenuItemRecipeDto(
            menuItem.Id,
            menuItem.PersianName,
            menuItem.EnglishName,
            menuItem.RecipeDescription,
            lines);
    }
}
