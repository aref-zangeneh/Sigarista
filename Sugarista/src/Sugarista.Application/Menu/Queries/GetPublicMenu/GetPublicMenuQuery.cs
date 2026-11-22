using Microsoft.EntityFrameworkCore;
using Sugarista.Application.MenuItems.Bundles;
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
    IReadOnlyCollection<PublicMenuItemOptionGroupDto> OptionGroups,
    IReadOnlyCollection<PublicMenuItemContentDto> Contents,
    bool IsBundle = false,
    decimal? BundleRegularTotal = null,
    decimal? BundleSavingsAmount = null,
    int? BundleSavingsPercent = null,
    IReadOnlyCollection<PublicMenuItemBundleComponentDto>? BundleItems = null);

public record PublicMenuItemBundleComponentDto(
    Guid MenuItemId,
    string PersianName,
    string EnglishName,
    string? ImageUrl,
    string Accent,
    string Short,
    string? SizeKey,
    string? SizeLabel,
    int Quantity,
    decimal UnitPrice,
    decimal LinePrice,
    bool IsAvailable);

/// <summary>
/// Customer-facing contents of an item: the Persian/English names of the raw
/// ingredients the admin chose to disclose. Quantities and preparation notes
/// stay internal — customers never see them.
/// </summary>
public record PublicMenuItemContentDto(
    string PersianName,
    string? EnglishName);

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
                item.DisplayOrder,
                item.IsBundle
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

        // Customer-visible contents: the names of ingredients the admin chose to
        // disclose on the item's recipe. Packaging and hidden rows are never
        // sent; quantities and preparation notes stay internal to the staff.
        IReadOnlyDictionary<Guid, PublicMenuItemContentDto[]> contentsByItem;
        try
        {
            var contentRows = await _dbContext.RecipeItems
                .AsNoTracking()
                .Where(recipeItem => recipeItem.ShowToCustomer && recipeItem.Ingredient.Kind == "ingredient")
                .OrderBy(recipeItem => recipeItem.Ingredient.DisplayOrder)
                .ThenBy(recipeItem => recipeItem.Ingredient.PersianName)
                .Select(recipeItem => new
                {
                    recipeItem.MenuItemId,
                    PersianName = recipeItem.Ingredient.PersianName,
                    recipeItem.Ingredient.EnglishName
                })
                .ToListAsync(cancellationToken);

            contentsByItem = contentRows
                .GroupBy(row => row.MenuItemId)
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .Select(row => new PublicMenuItemContentDto(row.PersianName, row.EnglishName))
                        .ToArray());
        }
        catch (Exception)
        {
            // Keep /api/menu working on databases that have not received the
            // RecipeItems.ShowToCustomer migration yet.
            contentsByItem = new Dictionary<Guid, PublicMenuItemContentDto[]>();
        }

        PublicMenuItemContentDto[] ContentsFor(Guid menuItemId) =>
            contentsByItem.TryGetValue(menuItemId, out var contents)
                ? [.. contents]
                : [];

        // Product bundles: load all bundle component rows in a single batch query
        // so each bundle exposes the real live standalone price of every component
        // alongside the bundle's selling price and exact customer savings.
        IReadOnlyDictionary<Guid, List<MenuItemBundleItem>> bundleItemsByBundle;
        try
        {
            var bundleMenuIds = menuRows.Where(r => r.IsBundle).Select(r => r.Id).ToList();
            if (bundleMenuIds.Count > 0)
            {
                var bundleRows = await _dbContext.MenuItemBundleItems
                    .AsNoTracking()
                    .Include(b => b.ComponentMenuItem)
                    .Where(b => bundleMenuIds.Contains(b.BundleMenuItemId))
                    .OrderBy(b => b.DisplayOrder)
                    .ThenBy(b => b.Id)
                    .ToListAsync(cancellationToken);

                bundleItemsByBundle = bundleRows
                    .GroupBy(b => b.BundleMenuItemId)
                    .ToDictionary(g => g.Key, g => g.ToList());
            }
            else
            {
                bundleItemsByBundle = new Dictionary<Guid, List<MenuItemBundleItem>>();
            }
        }
        catch (Exception)
        {
            bundleItemsByBundle = new Dictionary<Guid, List<MenuItemBundleItem>>();
        }

        IReadOnlyDictionary<Guid, MenuItemStock> menuItemStocksByItem;
        try
        {
            var stockRows = await _dbContext.MenuItemStocks
                .AsNoTracking()
                .ToListAsync(cancellationToken);
            menuItemStocksByItem = stockRows.ToDictionary(s => s.MenuItemId);
        }
        catch (Exception)
        {
            menuItemStocksByItem = new Dictionary<Guid, MenuItemStock>();
        }

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
                var variants = new[]
                {
                    new PublicMenuItemVariantDto("small", item.SmallName ?? "کوچک", item.SmallPrice, item.SmallSpecialOfferPrice, item.SmallPrice.HasValue ? computeDiscount(item.Id, item.CategoryId, item.SmallPrice.Value).discountedPrice : null, item.SmallPrice.HasValue ? computeDiscount(item.Id, item.CategoryId, item.SmallPrice.Value).percent : null, item.SmallVolume),
                    new PublicMenuItemVariantDto("medium", item.MediumName ?? "متوسط", item.MediumPrice, item.MediumSpecialOfferPrice, item.MediumPrice.HasValue ? computeDiscount(item.Id, item.CategoryId, item.MediumPrice.Value).discountedPrice : null, item.MediumPrice.HasValue ? computeDiscount(item.Id, item.CategoryId, item.MediumPrice.Value).percent : null, item.MediumVolume),
                    new PublicMenuItemVariantDto("large", item.LargeName ?? "بزرگ", item.LargePrice, item.LargeSpecialOfferPrice, item.LargePrice.HasValue ? computeDiscount(item.Id, item.CategoryId, item.LargePrice.Value).discountedPrice : null, item.LargePrice.HasValue ? computeDiscount(item.Id, item.CategoryId, item.LargePrice.Value).percent : null, item.LargeVolume)
                }.Where(variant => variant.Price.HasValue).ToArray();

                decimal? bundleRegularTotal = null;
                decimal? bundleSavingsAmount = null;
                int? bundleSavingsPercent = null;
                PublicMenuItemBundleComponentDto[]? bundleDtos = null;
                var effectiveOrderable = item.IsOrderable &&
                    (!menuItemStocksByItem.TryGetValue(item.Id, out var ownStock) || ownStock.CurrentQuantity > 0m);
                var effectiveCalories = item.Calories;
                var effectivePrepMinutes = item.PreparingDurationMinutes;

                if (item.IsBundle)
                {
                    var rawComponents = bundleItemsByBundle.GetValueOrDefault(item.Id) ?? [];
                    var effectiveBundlePrice = dp ?? item.SpecialOfferPrice ?? (variants.Length > 0 ? (variants[0].DiscountedPrice ?? variants[0].SpecialOfferPrice ?? variants[0].Price ?? item.Price) : item.Price);
                    decimal regTotal = 0m;
                    var allAvailable = rawComponents.Count > 0;
                    var list = new List<PublicMenuItemBundleComponentDto>(rawComponents.Count);
                    var sumCalories = 0;
                    var hasCalories = false;
                    var maxPrep = 0;

                    foreach (var entry in rawComponents)
                    {
                        if (entry.ComponentMenuItem is null)
                        {
                            allAvailable = false;
                            continue;
                        }

                        var comp = entry.ComponentMenuItem;
                        var (sizeKey, sizeLabel, unitPrice) = BundlePricingService.ResolveComponentUnitPrice(comp, entry.SizeKey);
                        var linePrice = unitPrice * entry.Quantity;
                        var compStockOk = !menuItemStocksByItem.TryGetValue(comp.Id, out var compStock) ||
                            compStock.CurrentQuantity >= entry.Quantity;
                        var compAvailable = comp.IsActive && comp.IsOrderable && unitPrice > 0m && compStockOk;
                        if (!compAvailable)
                        {
                            allAvailable = false;
                        }

                        regTotal += linePrice;
                        if (comp.Calories.HasValue)
                        {
                            sumCalories += comp.Calories.Value * entry.Quantity;
                            hasCalories = true;
                        }

                        if (comp.PreparingDurationMinutes.HasValue && comp.PreparingDurationMinutes.Value > maxPrep)
                        {
                            maxPrep = comp.PreparingDurationMinutes.Value;
                        }

                        list.Add(new PublicMenuItemBundleComponentDto(
                            comp.Id,
                            comp.PersianName,
                            comp.EnglishName,
                            comp.ImageUrl,
                            comp.Accent,
                            comp.ShortLabel,
                            sizeKey,
                            sizeLabel,
                            entry.Quantity,
                            unitPrice,
                            linePrice,
                            compAvailable));
                    }

                    bundleDtos = list.ToArray();
                    bundleRegularTotal = regTotal;
                    var savings = regTotal > effectiveBundlePrice ? regTotal - effectiveBundlePrice : 0m;
                    bundleSavingsAmount = savings;
                    bundleSavingsPercent = regTotal > 0m && savings > 0m
                        ? (int)Math.Round((savings / regTotal) * 100m, MidpointRounding.AwayFromZero)
                        : 0;

                    if (!allAvailable)
                    {
                        effectiveOrderable = false;
                    }

                    if (!effectiveCalories.HasValue && hasCalories)
                    {
                        effectiveCalories = sumCalories;
                    }

                    if (!effectivePrepMinutes.HasValue && maxPrep > 0)
                    {
                        effectivePrepMinutes = maxPrep;
                    }
                }

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
                effectiveOrderable,
                effectivePrepMinutes,
                effectiveCalories,
                item.Allergens,
                item.Volume,
                variants,
                item.Tags,
                item.Badge,
                item.Featured,
                item.Accent,
                item.Short,
                item.DisplayOrder,
                OptionGroupsFor(item.Id),
                ContentsFor(item.Id),
                item.IsBundle,
                bundleRegularTotal,
                bundleSavingsAmount,
                bundleSavingsPercent,
                bundleDtos);
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
