using Microsoft.EntityFrameworkCore;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Menu.Queries.GetPublicMenu;

public record GetPublicMenuQuery : IRequest<PublicMenuDto>;

public record PublicMenuDto(
    IReadOnlyCollection<PublicMenuCategoryDto> Categories,
    IReadOnlyCollection<PublicMenuItemDto> Items,
    IReadOnlyCollection<PublicAddonItemDto> Addons,
    IReadOnlyCollection<PublicMenuModifierDto> Modifiers,
    PublicFlashSaleDto? FlashSale);

public record PublicFlashSaleDto(
    string Title,
    DateTimeOffset EndsAt,
    decimal? DiscountPercent);

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
    decimal? DiscountedPrice,
    decimal? DiscountPercent,
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
    decimal? DiscountedPrice,
    decimal? DiscountPercent,
    string? Volume);

public record PublicAddonItemDto(
    Guid Id,
    string Name,
    string PersianName,
    string EnglishName,
    string Description,
    decimal Price,
    decimal? SpecialOfferPrice,
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
                CategoryId = item.Category.Id,
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

        var now = DateTimeOffset.UtcNow;

        var activeDiscounts = await _dbContext.Discounts
            .AsNoTracking()
            .Include(discount => discount.MenuItems)
            .Include(discount => discount.Categories)
            .Include(discount => discount.Schedules)
            .Where(discount => discount.IsActive && discount.IsAutomatic)
            .ToListAsync(cancellationToken);

        activeDiscounts = activeDiscounts
            .Where(discount => discount.IsLive(now) && discount.MatchesSchedule(now))
            .Where(discount => discount.CustomerGroup == DiscountCustomerGroup.All)
            .ToList();

        Func<Guid, Guid, decimal, (decimal? discountedPrice, decimal? percent)> computeDiscount = (menuItemId, categoryId, price) =>
        {
            if (price <= 0) return (null, null);

            decimal? best = null;
            foreach (var discount in activeDiscounts)
            {
                if (discount.MinCartAmount > 0 && discount.MinCartAmount > price) continue;

                // scope check
                bool inScope;
                if (!discount.HasScopeEntries)
                {
                    // cart-wide discount: not a product discount
                    continue;
                }
                var itemIds = discount.MenuItems.Select(m => m.MenuItemId).ToHashSet();
                var categoryIds = discount.Categories.Select(c => c.CategoryId).ToHashSet();
                inScope = itemIds.Contains(menuItemId) || categoryIds.Contains(categoryId);
                if (!inScope) continue;

                decimal discounted;
                if (discount.ValueKind == DiscountValueKind.Percentage)
                {
                    discounted = Math.Round(price * (1 - discount.Value / 100m), 2, MidpointRounding.AwayFromZero);
                    if (discount.MaxDiscountAmount is { } cap)
                    {
                        var reduction = price - discounted;
                        if (reduction > cap) discounted = price - cap;
                    }
                }
                else
                {
                    discounted = Math.Max(0, price - discount.Value);
                }

                if (discounted < price && (best is null || discounted < best.Value))
                {
                    best = discounted;
                }
            }

            if (best is null) return (null, null);
            var percent = Math.Round((price - best.Value) / price * 100m, 0, MidpointRounding.AwayFromZero);
            return (best, percent);
        };

        var items = menuRows
            .Select(item =>
            {
                var (dp, pct) = computeDiscount(item.Id, item.CategoryId, item.Price);
                return new PublicMenuItemDto(
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
                dp,
                pct,
                item.TaxPercent,
                item.IsOrderable,
                item.PreparingDurationMinutes,
                item.Calories,
                item.Allergens,
                item.Volume,
                new[]
                {
                    new PublicMenuItemVariantDto("small", "کوچک", item.SmallPrice, item.SmallSpecialOfferPrice, item.SmallPrice.HasValue ? computeDiscount(item.Id, item.CategoryId, item.SmallPrice.Value).discountedPrice : null, item.SmallPrice.HasValue ? computeDiscount(item.Id, item.CategoryId, item.SmallPrice.Value).percent : null, item.SmallVolume),
                    new PublicMenuItemVariantDto("medium", "متوسط", item.MediumPrice, item.MediumSpecialOfferPrice, item.MediumPrice.HasValue ? computeDiscount(item.Id, item.CategoryId, item.MediumPrice.Value).discountedPrice : null, item.MediumPrice.HasValue ? computeDiscount(item.Id, item.CategoryId, item.MediumPrice.Value).percent : null, item.MediumVolume),
                    new PublicMenuItemVariantDto("large", "بزرگ", item.LargePrice, item.LargeSpecialOfferPrice, item.LargePrice.HasValue ? computeDiscount(item.Id, item.CategoryId, item.LargePrice.Value).discountedPrice : null, item.LargePrice.HasValue ? computeDiscount(item.Id, item.CategoryId, item.LargePrice.Value).percent : null, item.LargeVolume)
                }.Where(variant => variant.Price.HasValue).ToArray(),
                item.Tags,
                item.Badge,
                item.Featured,
                item.Accent,
                item.Short,
                item.DisplayOrder);
            })
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
                    addon.SpecialOfferPrice,
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

        var flashSaleDiscount = await _dbContext.Discounts
            .AsNoTracking()
            .Where(discount => discount.IsActive && discount.IsAutomatic && discount.IsFlashSale)
            .ToListAsync(cancellationToken);

        var now2 = DateTimeOffset.UtcNow;
        var flashSale = flashSaleDiscount
            .Where(discount => discount.IsLive(now2) && discount.MatchesSchedule(now2))
            .Where(discount => discount.EndsAt.HasValue)
            .OrderByDescending(discount => discount.Priority)
            .ThenBy(discount => discount.EndsAt)
            .FirstOrDefault();

        PublicFlashSaleDto? flashSaleDto = null;
        if (flashSale is not null && flashSale.EndsAt.HasValue)
        {
            decimal? percent = flashSale.ValueKind == DiscountValueKind.Percentage ? flashSale.Value : null;
            flashSaleDto = new PublicFlashSaleDto(
                flashSale.Name,
                flashSale.EndsAt.Value,
                percent);
        }

        return new PublicMenuDto(categories, items, addons, modifiers, flashSaleDto);
    }
}
