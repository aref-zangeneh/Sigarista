using Microsoft.EntityFrameworkCore;

namespace Sugarista.Application.Menu.Queries.GetPublicMenu;

public record GetPublicMenuQuery : IRequest<PublicMenuDto>;

public record PublicMenuDto(IReadOnlyCollection<PublicMenuCategoryDto> Categories, IReadOnlyCollection<PublicMenuItemDto> Items);

public record PublicMenuCategoryDto(Guid Id, string Slug, string Label, int DisplayOrder);

public record PublicMenuItemDto(
    Guid Id,
    string Name,
    string Category,
    string CategoryLabel,
    string Description,
    int Price,
    IReadOnlyCollection<string> Tags,
    string? Badge,
    bool Featured,
    string Accent,
    string Short,
    int DisplayOrder);

public class GetPublicMenuQueryHandler : IRequestHandler<GetPublicMenuQuery, PublicMenuDto>
{
    private readonly IApplicationDbContext _dbContext;

    public GetPublicMenuQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PublicMenuDto> Handle(GetPublicMenuQuery request, CancellationToken cancellationToken)
    {
        var categories = await _dbContext.Categories
            .AsNoTracking()
            .Where(category => category.IsActive)
            .OrderBy(category => category.DisplayOrder)
            .ThenBy(category => category.Name)
            .Select(category => new PublicMenuCategoryDto(
                category.Id,
                category.Slug,
                category.Name,
                category.DisplayOrder))
            .ToListAsync(cancellationToken);

        var items = await _dbContext.MenuItems
            .AsNoTracking()
            .Where(item => item.IsActive && item.Category.IsActive)
            .OrderBy(item => item.Category.DisplayOrder)
            .ThenBy(item => item.DisplayOrder)
            .ThenBy(item => item.Name)
            .Select(item => new PublicMenuItemDto(
                item.Id,
                item.Name,
                item.Category.Slug,
                item.Category.Name,
                item.Description,
                item.Price,
                item.Tags,
                item.Badge,
                item.Featured,
                item.Accent,
                item.ShortLabel,
                item.DisplayOrder))
            .ToListAsync(cancellationToken);

        return new PublicMenuDto(categories, items);
    }
}
