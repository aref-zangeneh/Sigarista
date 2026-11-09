using Microsoft.EntityFrameworkCore;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Menu.Queries.GetPublicMenu;

public record GetPublicMenuQuery : IRequest<PublicMenuDto>;

public record PublicMenuDto(
    IReadOnlyCollection<PublicMenuCategoryDto> Categories,
    IReadOnlyCollection<PublicMenuItemDto> Items,
    IReadOnlyCollection<PublicAddonItemDto> Addons,
    IReadOnlyCollection<PublicMenuModifierDto> Modifiers,
    PublicFlashSaleDto? FlashSale,
    IReadOnlyCollection<PublicDrinkBuilderProfileDto> DrinkBuilders);

public record PublicFlashSaleDto(
    string Title,
    DateTimeOffset EndsAt,
    decimal? DiscountPercent);

public record PublicMenuCategoryDto(Guid Id, string Slug, string Label, int DisplayOrder, string? ImageUrl);

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
    int DisplayOrder,
    IReadOnlyCollection<PublicMenuItemOptionGroupDto> OptionGroups);

public record PublicMenuItemOptionGroupDto(
    Guid Id,
    string PersianName,
    string? EnglishName,
    bool IsRequired,
    string SelectionMode,
    int MinSelections,
    int MaxSelections,
    int DisplayOrder,
    IReadOnlyCollection<PublicMenuItemOptionDto> Options);

public record PublicMenuItemOptionDto(
    Guid Id,
    string PersianName,
    string? EnglishName,
    decimal PriceDelta,
    bool IsDefault,
    int DisplayOrder,
    bool IsAvailable);

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

public record PublicDrinkBuilderProfileDto(
    Guid Id,
    Guid MenuItemId,
    string Name,
    IReadOnlyCollection<PublicDrinkBuilderGroupDto> Groups);

public record PublicDrinkBuilderGroupDto(
    Guid Id,
    string Name,
    string InputType,
    string SelectionMode,
    int MinSelections,
    int MaxSelections,
    bool IsRequired,
    int DisplayOrder,
    IReadOnlyCollection<PublicDrinkBuilderOptionDto> Options);

public record PublicDrinkBuilderOptionDto(
    Guid Id,
    string Name,
    string? EnglishName,
    string Unit,
    decimal PricePerUnit,
    decimal MinAmount,
    decimal MaxAmount,
    decimal Step,
    decimal DefaultAmount,
    bool IsDefault,
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
                category.DisplayOrder,
                category.ImageUrl))
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
            // IsAutomatic is an unmapped computed property, so the equivalent
            // "no code" test is written out for EF to translate.
            .Where(discount => discount.IsActive && string.IsNullOrEmpty(discount.Code))
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

        // Per-item option groups (TASK-013): active groups/options are exposed
        // alongside each item and every option's availability is computed from
        // the current ingredient stock so the popup can disable sold-out rows.
        IReadOnlyDictionary<Guid, List<PublicMenuItemOptionGroupDto>> optionGroupsByItem;
        try
        {
            var optionGroups = await _dbContext.MenuItemOptionGroups
                .AsNoTracking()
                .Include(group => group.Options)
                    .ThenInclude(option => option.Ingredients)
                        .ThenInclude(ingredientRow => ingredientRow.Ingredient)
                .Where(group => group.IsActive)
                .OrderBy(group => group.DisplayOrder)
                .ToListAsync(cancellationToken);

            var optionIngredientIds = optionGroups
                .SelectMany(group => group.Options)
                .SelectMany(option => option.Ingredients)
                .Select(ingredientRow => ingredientRow.IngredientId)
                .Distinct()
                .ToList();

            var stockByIngredient = await _dbContext.InventoryStocks
                .AsNoTracking()
                .Where(stock => optionIngredientIds.Contains(stock.IngredientId))
                .ToDictionaryAsync(stock => stock.IngredientId, cancellationToken);

            optionGroupsByItem = optionGroups
                .GroupBy(group => group.MenuItemId)
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .Select(g => new PublicMenuItemOptionGroupDto(
                            g.Id,
                            g.PersianName,
                            g.EnglishName,
                            g.IsRequired,
                            g.SelectionMode,
                            g.MinSelections,
                            g.MaxSelections,
                            g.DisplayOrder,
                            g.Options
                                .Where(option => option.IsActive)
                                .OrderBy(option => option.DisplayOrder)
                                .ThenBy(option => option.PersianName, StringComparer.Ordinal)
                                .Select(option => new PublicMenuItemOptionDto(
                                    option.Id,
                                    option.PersianName,
                                    option.EnglishName,
                                    option.PriceDelta,
                                    option.IsDefault,
                                    option.DisplayOrder,
                                    IsOptionAvailable(option, stockByIngredient)))
                                .ToList()))
                        .ToList());
        }
        catch (Exception)
        {
            // Keep /api/menu working on databases that have not received the
            // option groups migration yet.
            optionGroupsByItem = new Dictionary<Guid, List<PublicMenuItemOptionGroupDto>>();
        }

        PublicMenuItemOptionGroupDto[] OptionGroupsFor(Guid menuItemId) =>
            optionGroupsByItem.TryGetValue(menuItemId, out var groups)
                ? [.. groups]
                : [];

        // An option is orderable only when every one of its ingredient usages
        // can be satisfied once (per single item unit) from current stock.
        static bool IsOptionAvailable(
            MenuItemOption option,
            IReadOnlyDictionary<Guid, InventoryStock> stockByIngredient)
        {
            if (option.Ingredients.Count == 0)
            {
                return true;
            }

            return option.Ingredients.All(ingredientRow =>
                stockByIngredient.TryGetValue(ingredientRow.IngredientId, out var stock) &&
                stock.CurrentQuantity >= ingredientRow.Quantity);
        }

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
                    new PublicMenuItemVariantDto("small", item.SmallName ?? "کوچک", item.SmallPrice, item.SmallSpecialOfferPrice, item.SmallPrice.HasValue ? computeDiscount(item.Id, item.CategoryId, item.SmallPrice.Value).discountedPrice : null, item.SmallPrice.HasValue ? computeDiscount(item.Id, item.CategoryId, item.SmallPrice.Value).percent : null, item.SmallVolume),
                    new PublicMenuItemVariantDto("medium", item.MediumName ?? "متوسط", item.MediumPrice, item.MediumSpecialOfferPrice, item.MediumPrice.HasValue ? computeDiscount(item.Id, item.CategoryId, item.MediumPrice.Value).discountedPrice : null, item.MediumPrice.HasValue ? computeDiscount(item.Id, item.CategoryId, item.MediumPrice.Value).percent : null, item.MediumVolume),
                    new PublicMenuItemVariantDto("large", item.LargeName ?? "بزرگ", item.LargePrice, item.LargeSpecialOfferPrice, item.LargePrice.HasValue ? computeDiscount(item.Id, item.CategoryId, item.LargePrice.Value).discountedPrice : null, item.LargePrice.HasValue ? computeDiscount(item.Id, item.CategoryId, item.LargePrice.Value).percent : null, item.LargeVolume)
                }.Where(variant => variant.Price.HasValue).ToArray(),
                item.Tags,
                item.Badge,
                item.Featured,
                item.Accent,
                item.Short,
                item.DisplayOrder,
                OptionGroupsFor(item.Id));
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

        // The custom drink builder is entirely optional: when the global feature
        // flag is off (or no profile exists) the theme receives an empty list and
        // hides every related control.
        List<PublicDrinkBuilderProfileDto> drinkBuilders;

        try
        {
            var builderEnabled = await _dbContext.SiteSettings
                .AsNoTracking()
                .Select(settings => (bool?)settings.CustomDrinkBuilderEnabled)
                .FirstOrDefaultAsync(cancellationToken) ?? true;

            if (builderEnabled)
            {
                var profiles = await _dbContext.DrinkBuilderProfiles
                    .AsNoTracking()
                    .Include(profile => profile.Groups)
                    .ThenInclude(group => group.Options)
                    .Where(profile => profile.IsEnabled)
                    .OrderBy(profile => profile.DisplayOrder)
                    .ThenBy(profile => profile.Name)
                    .ToListAsync(cancellationToken);

                drinkBuilders = profiles
                    .Where(profile => profile.Groups.Any(group => group.Options.Any(option => option.IsActive)))
                    .Select(profile => new PublicDrinkBuilderProfileDto(
                        profile.Id,
                        profile.MenuItemId,
                        profile.Name,
                        profile.Groups
                            .OrderBy(group => group.DisplayOrder)
                            .ThenBy(group => group.Name)
                            .Select(group => new PublicDrinkBuilderGroupDto(
                                group.Id,
                                group.Name,
                                group.InputType.ToString(),
                                group.SelectionMode.ToString(),
                                group.MinSelections,
                                group.MaxSelections,
                                group.IsRequired,
                                group.DisplayOrder,
                                group.Options
                                    .Where(option => option.IsActive)
                                    .OrderBy(option => option.DisplayOrder)
                                    .ThenBy(option => option.Name)
                                    .Select(option => new PublicDrinkBuilderOptionDto(
                                        option.Id,
                                        option.Name,
                                        option.EnglishName,
                                        option.Unit.ToString(),
                                        option.PricePerUnit,
                                        option.MinAmount,
                                        option.MaxAmount,
                                        option.Step,
                                        option.DefaultAmount,
                                        option.IsDefault,
                                        option.DisplayOrder))
                                    .ToList()))
                            .ToList()))
                    .ToList();
            }
            else
            {
                drinkBuilders = [];
            }
        }
        catch (Exception)
        {
            // Keep /api/menu working on databases that have not received the
            // Drink Builder migration yet.
            drinkBuilders = [];
        }

        var flashSaleDiscount = await _dbContext.Discounts
            .AsNoTracking()
            .Where(discount => discount.IsActive && string.IsNullOrEmpty(discount.Code) && discount.IsFlashSale)
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

        return new PublicMenuDto(categories, items, addons, modifiers, flashSaleDto, drinkBuilders);
    }
}
