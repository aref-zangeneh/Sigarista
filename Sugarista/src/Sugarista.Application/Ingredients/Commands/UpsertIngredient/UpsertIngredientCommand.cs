using Microsoft.EntityFrameworkCore;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Ingredients.Commands.UpsertIngredient;

public record UpsertIngredientCommand(
    Guid? Id,
    string PersianName,
    string EnglishName,
    string Unit,
    string Description,
    int DisplayOrder,
    bool IsActive,
    string InventoryLocation = "main") : IRequest<Guid>;

public class UpsertIngredientCommandValidator : AbstractValidator<UpsertIngredientCommand>
{
    public UpsertIngredientCommandValidator()
    {
        RuleFor(c => c.PersianName).NotEmpty().MaximumLength(200);
        RuleFor(c => c.EnglishName).NotEmpty().MaximumLength(200);
        RuleFor(c => c.Unit).NotEmpty().Must(u => Ingredient.ValidUnits.Contains(u, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"Unit must be one of: {string.Join(", ", Ingredient.ValidUnits)}.");
        RuleFor(c => c.InventoryLocation).NotEmpty()
            .Must(loc => Ingredient.ValidLocations.Contains(loc, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"Inventory location must be one of: {string.Join(", ", Ingredient.ValidLocations)}.");
        RuleFor(c => c.Description).MaximumLength(500);
        RuleFor(c => c.DisplayOrder).GreaterThanOrEqualTo(0);
    }
}

public class UpsertIngredientCommandHandler : IRequestHandler<UpsertIngredientCommand, Guid>
{
    private readonly IApplicationDbContext _dbContext;

    public UpsertIngredientCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Guid> Handle(UpsertIngredientCommand request, CancellationToken cancellationToken)
    {
        if (request.Id is { } ingredientId)
        {
            var existing = await _dbContext.Ingredients
                .FirstOrDefaultAsync(i => i.Id == ingredientId, cancellationToken)
                ?? throw new KeyNotFoundException("Ingredient was not found.");

            existing.Update(
                request.PersianName,
                request.EnglishName,
                request.Unit,
                request.Description,
                request.DisplayOrder,
                request.IsActive,
                request.InventoryLocation);

            await _dbContext.SaveChangesAsync(cancellationToken);
            return existing.Id;
        }

        var ingredient = new Ingredient(
            request.PersianName,
            request.EnglishName,
            request.Unit,
            request.Description,
            request.DisplayOrder,
            request.IsActive,
            request.InventoryLocation);

        _dbContext.Ingredients.Add(ingredient);

        // Create the stock record for the new ingredient automatically
        var stock = new Domain.Entities.InventoryStock(ingredient.Id, minAlertQuantity: 0);
        _dbContext.InventoryStocks.Add(stock);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ingredient.Id;
    }
}
