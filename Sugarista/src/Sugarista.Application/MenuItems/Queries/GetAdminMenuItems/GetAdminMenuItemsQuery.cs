using Microsoft.EntityFrameworkCore;

namespace Sugarista.Application.MenuItems.Queries.GetAdminMenuItems;

public record GetAdminMenuItemsQuery : IRequest<IReadOnlyCollection<AdminMenuItemDto>>;

public record AdminMenuItemDto(
    Guid Id,
    Guid CategoryId,
    string CategoryName,
    string CategorySlug,
    string Name,
    string PersianName,
    string EnglishName,
    string Description,
    int Price,
    string? ImageUrl,
    IReadOnlyCollection<string> GalleryImageUrls,
    int? SpecialOfferPrice,
    decimal TaxPercent,
    bool IsOrderable,
    int? PreparingDurationMinutes,
    int? Calories,
    IReadOnlyCollection<string> Allergens,
    string? Volume,
    int? SmallPrice,
    int? SmallSpecialOfferPrice,
    string? SmallVolume,
    int? MediumPrice,
    int? MediumSpecialOfferPrice,
    string? MediumVolume,
    int? LargePrice,
    int? LargeSpecialOfferPrice,
    string? LargeVolume,
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
                item.PersianName,
                item.EnglishName,
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
                item.ShortLabel,
                item.DisplayOrder,
                item.IsActive))
            .ToListAsync(cancellationToken);
    }
}
