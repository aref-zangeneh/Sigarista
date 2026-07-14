using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Extensions;
using Sugarista.Application.Common.Models;

namespace Sugarista.Application.Ingredients.Queries.GetAdminIngredients;

public record GetAdminIngredientsQuery(int Page = 1, int PageSize = 25, string? Search = null)
    : IRequest<PaginatedList<AdminIngredientDto>>;

public record AdminIngredientDto(
    Guid Id,
    string PersianName,
    string EnglishName,
    string Unit,
    string Description,
    int DisplayOrder,
    bool IsActive,
    string InventoryLocation);

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

        if (!string.IsNullOrWhiteSpace(pagination.Search))
        {
            var search = pagination.Search.Trim().ToLowerInvariant();
            query = query.Where(ingredient =>
                ingredient.PersianName.ToLower().Contains(search) ||
                ingredient.EnglishName.ToLower().Contains(search));
        }

        return await query
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
                i.InventoryLocation))
            .ToPaginatedListAsync(pagination, cancellationToken);
    }
}
