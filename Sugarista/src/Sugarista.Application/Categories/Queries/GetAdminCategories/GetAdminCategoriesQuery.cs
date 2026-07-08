using Microsoft.EntityFrameworkCore;

namespace Sugarista.Application.Categories.Queries.GetAdminCategories;

public record GetAdminCategoriesQuery : IRequest<IReadOnlyCollection<AdminCategoryDto>>;

public record AdminCategoryDto(Guid Id, string Slug, string Name, int DisplayOrder, bool IsActive, int ItemCount);

public class GetAdminCategoriesQueryHandler : IRequestHandler<GetAdminCategoriesQuery, IReadOnlyCollection<AdminCategoryDto>>
{
    private readonly IApplicationDbContext _dbContext;

    public GetAdminCategoriesQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<AdminCategoryDto>> Handle(GetAdminCategoriesQuery request, CancellationToken cancellationToken)
    {
        return await _dbContext.Categories
            .AsNoTracking()
            .OrderBy(category => category.DisplayOrder)
            .ThenBy(category => category.Name)
            .Select(category => new AdminCategoryDto(
                category.Id,
                category.Slug,
                category.Name,
                category.DisplayOrder,
                category.IsActive,
                category.MenuItems.Count))
            .ToListAsync(cancellationToken);
    }
}
