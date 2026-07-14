using Microsoft.EntityFrameworkCore;

namespace Sugarista.Application.ProductWiki.Queries.GetProductWiki;

public record GetProductWikiQuery(Guid MenuItemId) : IRequest<ProductWikiDto>;

public record ProductWikiDto(
    Guid MenuItemId,
    string MenuItemPersianName,
    string MenuItemEnglishName,
    string? Overview,
    string? QualityStandards,
    string? EquipmentNotes,
    string? ServingNotes,
    bool IsPublished,
    IReadOnlyCollection<ProductWikiStepDto> Steps,
    IReadOnlyCollection<ProductWikiRecipeLineDto> RecipeLines);

public record ProductWikiStepDto(
    Guid Id,
    int StepOrder,
    string Title,
    string Instructions,
    int? DurationMinutes,
    bool IsQualityCheckpoint);

public record ProductWikiRecipeLineDto(
    Guid Id,
    Guid IngredientId,
    string IngredientPersianName,
    string IngredientEnglishName,
    string Unit,
    decimal Quantity,
    string? PreparationNote);

public class GetProductWikiQueryHandler : IRequestHandler<GetProductWikiQuery, ProductWikiDto>
{
    private readonly IApplicationDbContext _dbContext;

    public GetProductWikiQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ProductWikiDto> Handle(GetProductWikiQuery request, CancellationToken cancellationToken)
    {
        var menuItem = await _dbContext.MenuItems
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == request.MenuItemId, cancellationToken)
            ?? throw new KeyNotFoundException("Menu item was not found.");

        var guide = await _dbContext.ProductWikiGuides
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.MenuItemId == request.MenuItemId, cancellationToken);

        var steps = await _dbContext.ProductWikiSteps
            .AsNoTracking()
            .Where(s => s.MenuItemId == request.MenuItemId)
            .OrderBy(s => s.StepOrder)
            .Select(s => new ProductWikiStepDto(
                s.Id,
                s.StepOrder,
                s.Title,
                s.Instructions,
                s.DurationMinutes,
                s.IsQualityCheckpoint))
            .ToListAsync(cancellationToken);

        var recipeLines = await _dbContext.RecipeItems
            .AsNoTracking()
            .Where(r => r.MenuItemId == request.MenuItemId)
            .OrderBy(r => r.Ingredient.PersianName)
            .Select(r => new ProductWikiRecipeLineDto(
                r.Id,
                r.IngredientId,
                r.Ingredient.PersianName,
                r.Ingredient.EnglishName,
                r.Ingredient.Unit,
                r.Quantity,
                r.PreparationNote))
            .ToListAsync(cancellationToken);

        return new ProductWikiDto(
            menuItem.Id,
            menuItem.PersianName,
            menuItem.EnglishName,
            guide?.Overview,
            guide?.QualityStandards,
            guide?.EquipmentNotes,
            guide?.ServingNotes,
            guide?.IsPublished ?? false,
            steps,
            recipeLines);
    }
}
