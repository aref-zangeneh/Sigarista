using Microsoft.EntityFrameworkCore;

namespace Sugarista.Application.Menu.Queries.GetPublicMenu;

public record GetPublicMenuQuery : IRequest<PublicMenuDto>;

public record PublicMenuDto(
    IReadOnlyCollection<PublicMenuCategoryDto> Categories,
    IReadOnlyCollection<PublicMenuItemDto> Items,
    IReadOnlyCollection<PublicAddonItemDto> Addons,
    IReadOnlyCollection<PublicMenuModifierDto> Modifiers);

public record PublicMenuCategoryDto(Guid Id, string Slug, string Label, int DisplayOrder);

public record PublicMenuItemDto(
    Guid Id,
    string Name,
    string PersianName,
    string EnglishName,
    string Category,
    string CategoryLabel,
    string Description,
    decimal Price,
    string? ImageUrl,
    IReadOnlyCollection<string> GalleryImageUrls,
    decimal? SpecialOfferPrice,
    decimal TaxPercent,
    bool IsOrderable,
    int? PreparingDurationMinutes,
    int? Calories,
    IReadOnlyCollection<string> Allergens,
    string? Volume,
    IReadOnlyCollection<PublicMenuItemVariantDto> Variants,
    IReadOnlyCollection<string> Tags,
    string? Badge,
    bool Featured,
    string Accent,
    string Short,
    int DisplayOrder);

public record PublicMenuItemVariantDto(
    string Key,
    string Label,
    decimal? Price,
    decimal? SpecialOfferPrice,
    string? Volume);

public record PublicAddonItemDto(
    Guid Id,
    string Name,
    string PersianName,
    string EnglishName,
    string Description,
    decimal Price,
    IReadOnlyCollection<string> Tags,
    int DisplayOrder);

public record PublicMenuModifierDto(
    Guid Id,
    string Name,
    string PersianName,
    string EnglishName,
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

        var menuRows = await _dbContext.MenuItems
            .AsNoTracking()
            .Where(item => item.IsActive && item.Category.IsActive)
            .OrderBy(item => item.Category.DisplayOrder)
            .ThenBy(item => item.DisplayOrder)
            .ThenBy(item => item.Name)
            .Select(item => new
            {
                item.Id,
                item.Name,
                item.PersianName,
                item.EnglishName,
                Category = item.Category.Slug,
                CategoryLabel = item.Category.Name,
                item.Description,
                item.Price,
                item.ImageUrl,
                item.GalleryImageUrls,
                item.SpecialOfferPrice,
                item.TaxPercent,
                item.IsOrderable,
                item.PreparingDurationMinutes,
                item.Calories,
                item.Allergens,
                item.Volume,
                item.SmallPrice,
                item.SmallSpecialOfferPrice,
                item.SmallVolume,
                item.MediumPrice,
                item.MediumSpecialOfferPrice,
                item.MediumVolume,
                item.LargePrice,
                item.LargeSpecialOfferPrice,
                item.LargeVolume,
                item.Tags,
                item.Badge,
                item.Featured,
                item.Accent,
                Short = item.ShortLabel,
                item.DisplayOrder
            })
            .ToListAsync(cancellationToken);

        var items = menuRows
            .Select(item => new PublicMenuItemDto(
                item.Id,
                item.Name,
                item.PersianName,
                item.EnglishName,
                item.Category,
                item.CategoryLabel,
                item.Description,
                item.Price,
                item.ImageUrl,
                item.GalleryImageUrls,
                item.SpecialOfferPrice,
                item.TaxPercent,
                item.IsOrderable,
                item.PreparingDurationMinutes,
                item.Calories,
                item.Allergens,
                item.Volume,
                new[]
                {
                    new PublicMenuItemVariantDto("small", "کوچک", item.SmallPrice, item.SmallSpecialOfferPrice, item.SmallVolume),
                    new PublicMenuItemVariantDto("medium", "متوسط", item.MediumPrice, item.MediumSpecialOfferPrice, item.MediumVolume),
                    new PublicMenuItemVariantDto("large", "بزرگ", item.LargePrice, item.LargeSpecialOfferPrice, item.LargeVolume)
                }.Where(variant => variant.Price.HasValue).ToArray(),
                item.Tags,
                item.Badge,
                item.Featured,
                item.Accent,
                item.Short,
                item.DisplayOrder))
            .ToList();

        List<PublicAddonItemDto> addons;

        try
        {
            addons = await _dbContext.AddonItems
                .AsNoTracking()
                .Where(addon => addon.IsActive)
                .OrderBy(addon => addon.DisplayOrder)
                .ThenBy(addon => addon.Name)
                .Select(addon => new PublicAddonItemDto(
                    addon.Id,
                    addon.Name,
                    addon.PersianName,
                    addon.EnglishName,
                    addon.Description,
                    addon.Price,
                    addon.Tags,
                    addon.DisplayOrder))
                .ToListAsync(cancellationToken);
        }
        catch (Exception)
        {
            // If the AddonItems table or data is not available in production yet,
            // fall back to an empty add-ons collection so /api/menu still works.
            addons = [];
        }

        var modifiers = await _dbContext.MenuModifiers
            .AsNoTracking()
            .Where(modifier => modifier.IsActive)
            .OrderBy(modifier => modifier.DisplayOrder)
            .ThenBy(modifier => modifier.Name)
            .Select(modifier => new PublicMenuModifierDto(
                modifier.Id,
                modifier.Name,
                modifier.PersianName,
                modifier.EnglishName,
                modifier.DisplayOrder))
            .ToListAsync(cancellationToken);

        return new PublicMenuDto(categories, items, addons, modifiers);
    }
}
