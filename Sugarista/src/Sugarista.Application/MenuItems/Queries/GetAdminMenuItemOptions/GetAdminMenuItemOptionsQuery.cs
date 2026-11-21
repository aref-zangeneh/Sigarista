using Microsoft.EntityFrameworkCore;
using Sugarista.Application.MenuItems.Bundles;

namespace Sugarista.Application.MenuItems.Queries.GetAdminMenuItemOptions;

public record GetAdminMenuItemOptionsQuery : IRequest<IReadOnlyCollection<AdminMenuItemOptionDto>>;

public record AdminMenuItemOptionDto(
    Guid Id,
    string PersianName,
    string EnglishName,
    bool IsActive,
    bool IsBundle = false,
    decimal EffectivePrice = 0m,
    decimal? SmallPrice = null,
    string? SmallName = null,
    decimal? MediumPrice = null,
    string? MediumName = null,
    decimal? LargePrice = null,
    string? LargeName = null,
    string? ImageUrl = null);

public class GetAdminMenuItemOptionsQueryHandler : IRequestHandler<GetAdminMenuItemOptionsQuery, IReadOnlyCollection<AdminMenuItemOptionDto>>
{
    private readonly IApplicationDbContext _dbContext;

    public GetAdminMenuItemOptionsQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<AdminMenuItemOptionDto>> Handle(
        GetAdminMenuItemOptionsQuery request,
        CancellationToken cancellationToken)
    {
        var items = await _dbContext.MenuItems
            .AsNoTracking()
            .OrderBy(item => item.DisplayOrder)
            .ThenBy(item => item.PersianName)
            .ToListAsync(cancellationToken);

        return items
            .Select(item => new AdminMenuItemOptionDto(
                item.Id,
                item.PersianName,
                item.EnglishName,
                item.IsActive,
                item.IsBundle,
                BundlePricingService.ResolveEffectiveSellingPrice(item),
                item.SmallPrice.HasValue ? (item.SmallSpecialOfferPrice ?? item.SmallPrice) : null,
                item.SmallName ?? "کوچک",
                item.MediumPrice.HasValue ? (item.MediumSpecialOfferPrice ?? item.MediumPrice) : null,
                item.MediumName ?? "متوسط",
                item.LargePrice.HasValue ? (item.LargeSpecialOfferPrice ?? item.LargePrice) : null,
                item.LargeName ?? "بزرگ",
                item.ImageUrl))
            .ToList();
    }
}
