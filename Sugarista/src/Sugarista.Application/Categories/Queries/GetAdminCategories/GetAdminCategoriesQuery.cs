using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Extensions;
using Sugarista.Application.Common.Models;

namespace Sugarista.Application.Categories.Queries.GetAdminCategories;

public record GetAdminCategoriesQuery(int Page = 1, int PageSize = 25, string? Search = null)
    : IRequest<PaginatedList<AdminCategoryDto>>;

public record AdminCategoryDto(Guid Id, string Slug, string Name, int DisplayOrder, bool IsActive, int ItemCount);

public class GetAdminCategoriesQueryHandler : IRequestHandler<GetAdminCategoriesQuery, PaginatedList<AdminCategoryDto>>
{
    private readonly IApplicationDbContext _dbContext;

    public GetAdminCategoriesQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PaginatedList<AdminCategoryDto>> Handle(GetAdminCategoriesQuery request, CancellationToken cancellationToken)
    {
        var pagination = new PaginationQuery(request.Page, request.PageSize, request.Search);

        var query = _dbContext.Categories.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(pagination.Search))
        {
            var search = pagination.Search.Trim().ToLowerInvariant();
            query = query.Where(category =>
                category.Name.ToLower().Contains(search) ||
                category.Slug.ToLower().Contains(search));
        }

        return await query
            .OrderBy(category => category.DisplayOrder)
            .ThenBy(category => category.Name)
            .Select(category => new AdminCategoryDto(
                category.Id,
                category.Slug,
                category.Name,
                category.DisplayOrder,
                category.IsActive,
                category.MenuItems.Count))
            .ToPaginatedListAsync(pagination, cancellationToken);
    }
}
