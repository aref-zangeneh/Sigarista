using Microsoft.EntityFrameworkCore;

namespace Sugarista.Application.MenuItems.Queries.GetAdminMenuItems;

public record GetAdminMenuItemsQuery : IRequest<IReadOnlyCollection<AdminMenuItemDto>>;

public record AdminMenuItemDto(
    Guid Id,
    Guid CategoryId,
    string CategoryName,
    string CategorySlug,
    string Name,
    string Description,
    int Price,
    IReadOnlyCollection<string> Tags,
    string? Badge,
    bool Featured,
    string Accent,
    string ShortLabel,
    int DisplayOrder,
    bool IsActive);

public class GetAdminMenuItemsQueryHandler : IRequestHandler<GetAdminMenuItemsQuery, IReadOnlyCollection<AdminMenuItemDto>>
{
    private readonly IApplicationDbContext _dbContext;

    public GetAdminMenuItemsQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<AdminMenuItemDto>> Handle(GetAdminMenuItemsQuery request, CancellationToken cancellationToken)
    {
        return await _dbContext.MenuItems
            .AsNoTracking()
            .OrderBy(item => item.Category.DisplayOrder)
            .ThenBy(item => item.DisplayOrder)
            .ThenBy(item => item.Name)
            .Select(item => new AdminMenuItemDto(
                item.Id,
                item.CategoryId,
                item.Category.Name,
                item.Category.Slug,
                item.Name,
                item.Description,
                item.Price,
                item.Tags,
                item.Badge,
                item.Featured,
                item.Accent,
                item.ShortLabel,
                item.DisplayOrder,
                item.IsActive))
            .ToListAsync(cancellationToken);
    }
}
