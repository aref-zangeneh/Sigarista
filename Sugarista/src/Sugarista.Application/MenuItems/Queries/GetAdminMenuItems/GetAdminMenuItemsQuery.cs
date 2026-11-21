using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Extensions;
using Sugarista.Application.Common.Models;
using Sugarista.Application.MenuItems.Bundles;

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
    string? SmallName,
    decimal? MediumPrice,
    decimal? MediumSpecialOfferPrice,
    string? MediumVolume,
    string? MediumName,
    decimal? LargePrice,
    decimal? LargeSpecialOfferPrice,
    string? LargeVolume,
    string? LargeName,
    IReadOnlyCollection<string> Tags,
    string? Badge,
    bool Featured,
    string Accent,
    string ShortLabel,
    int DisplayOrder,
    bool IsActive,
    bool IsBundle = false,
    int BundleItemCount = 0,
    decimal? BundleRegularTotal = null);

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

        var pageResult = await query
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
                item.SmallName,
                item.MediumPrice,
                item.MediumSpecialOfferPrice,
                item.MediumVolume,
                item.MediumName,
                item.LargePrice,
                item.LargeSpecialOfferPrice,
                item.LargeVolume,
                item.LargeName,
                item.Tags,
                item.Badge,
                item.Featured,
                item.Accent,
                item.ShortLabel,
                item.DisplayOrder,
                item.IsActive,
                item.IsBundle,
                0,
                null))
            .ToPaginatedListAsync(pagination, cancellationToken);

        var bundleIds = pageResult.Items
            .Where(item => item.IsBundle)
            .Select(item => item.Id)
            .ToList();

        if (bundleIds.Count == 0)
        {
            return pageResult;
        }

        var bundleLines = await _dbContext.MenuItemBundleItems
            .AsNoTracking()
            .Include(b => b.ComponentMenuItem)
            .Where(b => bundleIds.Contains(b.BundleMenuItemId))
            .ToListAsync(cancellationToken);

        var linesByBundle = bundleLines
            .GroupBy(b => b.BundleMenuItemId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var enriched = pageResult.Items
            .Select(item =>
            {
                if (!item.IsBundle || !linesByBundle.TryGetValue(item.Id, out var lines))
                {
                    return item;
                }

                var regularTotal = lines.Sum(entry =>
                {
                    if (entry.ComponentMenuItem is null) return 0m;
                    var (_, _, unitPrice) = BundlePricingService.ResolveComponentUnitPrice(entry.ComponentMenuItem, entry.SizeKey);
                    return unitPrice * entry.Quantity;
                });

                return item with
                {
                    BundleItemCount = lines.Sum(l => l.Quantity),
                    BundleRegularTotal = regularTotal
                };
            })
            .ToArray();

        return new PaginatedList<AdminMenuItemDto>(enriched, pageResult.TotalCount, pageResult.Page, pageResult.PageSize);
    }
}
