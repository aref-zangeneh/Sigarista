using Microsoft.EntityFrameworkCore;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Recipes.Commands.UpsertRecipeItem;

public record UpsertRecipeItemCommand(
    Guid MenuItemId,
    Guid IngredientId,
    decimal Quantity,
    string? PreparationNote = null) : IRequest<Guid>;

public class UpsertRecipeItemCommandValidator : AbstractValidator<UpsertRecipeItemCommand>
{
    public UpsertRecipeItemCommandValidator()
    {
        RuleFor(c => c.MenuItemId).NotEmpty();
        RuleFor(c => c.IngredientId).NotEmpty();
        RuleFor(c => c.Quantity).GreaterThan(0);
        RuleFor(c => c.PreparationNote).MaximumLength(1000).When(c => c.PreparationNote is not null);
    }
}

public class UpsertRecipeItemCommandHandler : IRequestHandler<UpsertRecipeItemCommand, Guid>
{
    private readonly IApplicationDbContext _dbContext;

    public UpsertRecipeItemCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Guid> Handle(UpsertRecipeItemCommand request, CancellationToken cancellationToken)
    {
        var menuItemExists = await _dbContext.MenuItems
            .AnyAsync(m => m.Id == request.MenuItemId, cancellationToken);

        if (!menuItemExists)
        {
            throw new KeyNotFoundException("Menu item was not found.");
        }

        var ingredientExists = await _dbContext.Ingredients
            .AnyAsync(i => i.Id == request.IngredientId, cancellationToken);

        if (!ingredientExists)
        {
            throw new KeyNotFoundException("Ingredient was not found.");
        }

        // Upsert: update quantity if this ingredient is already in the recipe
        var existing = await _dbContext.RecipeItems
            .FirstOrDefaultAsync(r => r.MenuItemId == request.MenuItemId && r.IngredientId == request.IngredientId, cancellationToken);

        if (existing is not null)
        {
            existing.Update(request.MenuItemId, request.IngredientId, request.Quantity, request.PreparationNote);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return existing.Id;
        }

        var recipeItem = new RecipeItem(
            request.MenuItemId,
            request.IngredientId,
            request.Quantity,
            request.PreparationNote);
        _dbContext.RecipeItems.Add(recipeItem);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return recipeItem.Id;
    }
}
