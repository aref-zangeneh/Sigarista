using Microsoft.EntityFrameworkCore;

namespace Sugarista.Application.Recipes.Queries.GetMenuItemRecipe;

public record GetMenuItemRecipeQuery(Guid MenuItemId) : IRequest<IReadOnlyCollection<RecipeItemDto>>;

public record RecipeItemDto(
    Guid Id,
    Guid IngredientId,
    string IngredientPersianName,
    string IngredientEnglishName,
    string Unit,
    decimal Quantity);

public class GetMenuItemRecipeQueryHandler : IRequestHandler<GetMenuItemRecipeQuery, IReadOnlyCollection<RecipeItemDto>>
{
    private readonly IApplicationDbContext _dbContext;

    public GetMenuItemRecipeQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<RecipeItemDto>> Handle(GetMenuItemRecipeQuery request, CancellationToken cancellationToken)
    {
        var menuItemExists = await _dbContext.MenuItems
            .AnyAsync(m => m.Id == request.MenuItemId, cancellationToken);

        if (!menuItemExists)
        {
            throw new KeyNotFoundException("Menu item was not found.");
        }

        return await _dbContext.RecipeItems
            .AsNoTracking()
            .Where(r => r.MenuItemId == request.MenuItemId)
            .Select(r => new RecipeItemDto(
                r.Id,
                r.IngredientId,
                r.Ingredient.PersianName,
                r.Ingredient.EnglishName,
                r.Ingredient.Unit,
                r.Quantity))
            .ToListAsync(cancellationToken);
    }
}
