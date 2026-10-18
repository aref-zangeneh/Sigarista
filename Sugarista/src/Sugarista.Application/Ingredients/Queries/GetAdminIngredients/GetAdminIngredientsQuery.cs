using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Extensions;
using Sugarista.Application.Common.Models;

namespace Sugarista.Application.Ingredients.Queries.GetAdminIngredients;

public record GetAdminIngredientsQuery(
    int Page = 1,
    int PageSize = 25,
    string? Search = null,
    string? Kind = null)
    : IRequest<PaginatedList<AdminIngredientDto>>;

public record AdminIngredientDto(
    Guid Id,
    string PersianName,
    string EnglishName,
    string Unit,
    string Description,
    int DisplayOrder,
    bool IsActive,
    string InventoryLocation,
    decimal UnitCost,
    string Kind,
    IReadOnlyList<Guid> SupplierIds,
    IReadOnlyList<string> SupplierNames,
    Guid? PreferredSupplierId)
{
    public string SupplierNamesText =>
        SupplierNames.Count == 0 ? string.Empty : string.Join("، ", SupplierNames);
}

public class GetAdminIngredientsQueryHandler : IRequestHandler<GetAdminIngredientsQuery, PaginatedList<AdminIngredientDto>>
{
    private readonly IApplicationDbContext _dbContext;

    public GetAdminIngredientsQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PaginatedList<AdminIngredientDto>> Handle(GetAdminIngredientsQuery request, CancellationToken cancellationToken)
    {
        var pagination = new PaginationQuery(request.Page, request.PageSize, request.Search);

        var query = _dbContext.Ingredients.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Kind))
        {
            var kind = request.Kind.Trim().ToLowerInvariant();
            query = query.Where(ingredient => ingredient.Kind == kind);
        }

        if (!string.IsNullOrWhiteSpace(pagination.Search))
        {
            var search = pagination.Search.Trim().ToLowerInvariant();
            query = query.Where(ingredient =>
                ingredient.PersianName.ToLower().Contains(search) ||
                ingredient.EnglishName.ToLower().Contains(search));
        }

        var page = await query
            .OrderBy(i => i.DisplayOrder)
            .ThenBy(i => i.Name)
            .Select(i => new AdminIngredientDto(
                i.Id,
                i.PersianName,
                i.EnglishName,
                i.Unit,
                i.Description,
                i.DisplayOrder,
                i.IsActive,
                i.InventoryLocation,
                i.UnitCost,
                i.Kind,
                Array.Empty<Guid>(),
                Array.Empty<string>(),
                null))
            .ToPaginatedListAsync(pagination, cancellationToken);

        var ingredientIds = page.Items.Select(i => i.Id).ToList();

        var links = await _dbContext.IngredientSuppliers
            .AsNoTracking()
            .Include(link => link.Supplier)
            .Where(link => ingredientIds.Contains(link.IngredientId))
            .OrderByDescending(link => link.IsPreferred)
            .ThenBy(link => link.Supplier.PersianName)
            .ToListAsync(cancellationToken);

        if (links.Count == 0)
        {
            return page;
        }

        var byIngredient = links
            .GroupBy(link => link.IngredientId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<Domain.Entities.IngredientSupplier>)group.ToList());

        var items = page.Items
            .Select(item =>
            {
                if (!byIngredient.TryGetValue(item.Id, out var ingredientLinks))
                {
                    return item;
                }

                var preferred = ingredientLinks.FirstOrDefault(link => link.IsPreferred);

                return item with
                {
                    SupplierIds = ingredientLinks.Select(link => link.SupplierId).ToList(),
                    SupplierNames = ingredientLinks.Select(link => link.Supplier.PersianName).ToList(),
                    PreferredSupplierId = preferred?.SupplierId,
                };
            })
            .ToList();

        return page with { Items = items };
    }
}
