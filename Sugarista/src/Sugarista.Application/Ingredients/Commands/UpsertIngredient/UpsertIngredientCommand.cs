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
    string InventoryLocation = "main",
    decimal UnitCost = 0,
    string Kind = "ingredient",
    IReadOnlyList<Guid>? SupplierIds = null,
    Guid? PreferredSupplierId = null) : IRequest<Guid>;

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
        RuleFor(c => c.Kind).NotEmpty()
            .Must(kind => Ingredient.ValidKinds.Contains(kind, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"Kind must be one of: {string.Join(", ", Ingredient.ValidKinds)}.");
        RuleFor(c => c.Description).MaximumLength(500);
        RuleFor(c => c.DisplayOrder).GreaterThanOrEqualTo(0);
        RuleFor(c => c.UnitCost).GreaterThanOrEqualTo(0);

        RuleFor(c => c.SupplierIds)
            .Must(ids => ids == null || ids.All(id => id != Guid.Empty))
            .WithMessage("Supplier ids must be valid.");

        RuleFor(c => c)
            .Must(c => c.PreferredSupplierId == null
                || c.SupplierIds == null
                || c.SupplierIds.Contains(c.PreferredSupplierId.Value))
            .WithMessage("Preferred supplier must be one of the linked suppliers.");
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
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == ingredientId, cancellationToken)
                ?? throw new KeyNotFoundException("Ingredient was not found.");

            if (!string.Equals(existing.Kind, request.Kind, StringComparison.OrdinalIgnoreCase)
                && string.Equals(request.Kind, "packaging", StringComparison.OrdinalIgnoreCase))
            {
                var isUsedInRecipes = await _dbContext.RecipeItems
                    .AnyAsync(r => r.IngredientId == ingredientId, cancellationToken);

                if (isUsedInRecipes)
                {
                    throw new InvalidOperationException(
                        "This item is used in one or more recipes. Remove it from all recipes before changing its kind to packaging.");
                }
            }

            var tracked = _dbContext.Ingredients.First(i => i.Id == ingredientId);

            tracked.Update(
                request.PersianName,
                request.EnglishName,
                request.Unit,
                request.Description,
                request.DisplayOrder,
                request.IsActive,
                request.InventoryLocation,
                request.UnitCost,
                request.Kind);

            if (request.SupplierIds is not null)
            {
                SyncIngredientSuppliers(_dbContext, ingredientId, request.SupplierIds, request.PreferredSupplierId);
            }

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
            request.InventoryLocation,
            request.UnitCost,
            request.Kind);

        _dbContext.Ingredients.Add(ingredient);

        // Create the stock record for the new item automatically
        var stock = new Domain.Entities.InventoryStock(ingredient.Id, minAlertQuantity: 0);
        _dbContext.InventoryStocks.Add(stock);

        if (request.SupplierIds is { Count: > 0 } supplierIds)
        {
            foreach (var supplierId in supplierIds.Distinct())
            {
                _dbContext.IngredientSuppliers.Add(new IngredientSupplier(
                    ingredient.Id,
                    supplierId,
                    supplierId == request.PreferredSupplierId));
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ingredient.Id;
    }

    private static void SyncIngredientSuppliers(
        IApplicationDbContext dbContext,
        Guid ingredientId,
        IReadOnlyList<Guid> supplierIds,
        Guid? preferredSupplierId)
    {
        var desired = supplierIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        var links = dbContext.IngredientSuppliers
            .Where(link => link.IngredientId == ingredientId)
            .ToList();

        foreach (var link in links.Where(l => !desired.Contains(l.SupplierId)).ToList())
        {
            dbContext.IngredientSuppliers.Remove(link);
        }

        // Exactly one preferred supplier (first one when none was chosen).
        var preferredId = preferredSupplierId is { } preferred && desired.Contains(preferred)
            ? preferred
            : desired.FirstOrDefault();

        foreach (var supplierId in desired)
        {
            var link = links.FirstOrDefault(l => l.SupplierId == supplierId);
            var isPreferred = supplierId == preferredId;

            if (link is null)
            {
                dbContext.IngredientSuppliers.Add(new IngredientSupplier(ingredientId, supplierId, isPreferred));
            }
            else
            {
                link.SetPreferred(isPreferred);
            }
        }
    }
}
