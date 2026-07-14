using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Extensions;
using Sugarista.Application.Common.Models;

namespace Sugarista.Application.MenuItems.Queries.GetAdminMenuItems;

public record GetAdminMenuItemsQuery(int Page = 1, int PageSize = 25, string? Search = null)
    : IRequest<PaginatedList<AdminMenuItemDto>>;

public record AdminMenuItemDto(
    Guid Id,
    Guid CategoryId,
    string CategoryName,
    string CategorySlug,
    string Name,
    string PersianName,
    string EnglishName,
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
    decimal? SmallPrice,
    decimal? SmallSpecialOfferPrice,
    string? SmallVolume,
    decimal? MediumPrice,
    decimal? MediumSpecialOfferPrice,
    string? MediumVolume,
    decimal? LargePrice,
    decimal? LargeSpecialOfferPrice,
    string? LargeVolume,
    IReadOnlyCollection<string> Tags,
    string? Badge,
    bool Featured,
    string Accent,
    string ShortLabel,
    int DisplayOrder,
    bool IsActive);

public class GetAdminMenuItemsQueryHandler : IRequestHandler<GetAdminMenuItemsQuery, PaginatedList<AdminMenuItemDto>>
{
    private readonly IApplicationDbContext _dbContext;

    public GetAdminMenuItemsQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PaginatedList<AdminMenuItemDto>> Handle(GetAdminMenuItemsQuery request, CancellationToken cancellationToken)
    {
        var pagination = new PaginationQuery(request.Page, request.PageSize, request.Search);

        var query = _dbContext.MenuItems.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(pagination.Search))
        {
            var search = pagination.Search.Trim().ToLowerInvariant();
            query = query.Where(item =>
                item.Name.ToLower().Contains(search) ||
                item.PersianName.ToLower().Contains(search) ||
                item.EnglishName.ToLower().Contains(search));
        }

        return await query
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
            .ToPaginatedListAsync(pagination, cancellationToken);
    }
}
