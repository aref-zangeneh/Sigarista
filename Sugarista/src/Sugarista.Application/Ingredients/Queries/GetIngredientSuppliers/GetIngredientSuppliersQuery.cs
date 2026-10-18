using Microsoft.EntityFrameworkCore;

namespace Sugarista.Application.Ingredients.Queries.GetIngredientSuppliers;

public record GetIngredientSuppliersQuery(Guid IngredientId) : IRequest<IReadOnlyCollection<IngredientSupplierDto>>;

public record IngredientSupplierDto(
    Guid SupplierId,
    string SupplierName,
    string PhoneNumber,
    bool IsPreferred,
    bool IsActive);

public class GetIngredientSuppliersQueryHandler
    : IRequestHandler<GetIngredientSuppliersQuery, IReadOnlyCollection<IngredientSupplierDto>>
{
    private readonly IApplicationDbContext _dbContext;

    public GetIngredientSuppliersQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<IngredientSupplierDto>> Handle(
        GetIngredientSuppliersQuery request,
        CancellationToken cancellationToken)
    {
        var ingredientExists = await _dbContext.Ingredients
            .AsNoTracking()
            .AnyAsync(i => i.Id == request.IngredientId, cancellationToken);

        if (!ingredientExists)
        {
            throw new KeyNotFoundException("Ingredient was not found.");
        }

        return await _dbContext.IngredientSuppliers
            .AsNoTracking()
            .Where(link => link.IngredientId == request.IngredientId)
            .OrderByDescending(link => link.IsPreferred)
            .ThenBy(link => link.Supplier.PersianName)
            .Select(link => new IngredientSupplierDto(
                link.SupplierId,
                link.Supplier.PersianName,
                link.Supplier.PhoneNumber,
                link.IsPreferred,
                link.Supplier.IsActive))
            .ToListAsync(cancellationToken);
    }
}
