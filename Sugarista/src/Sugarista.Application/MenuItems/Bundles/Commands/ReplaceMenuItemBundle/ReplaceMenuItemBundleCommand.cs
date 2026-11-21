using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Domain.Entities;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Application.MenuItems.Bundles.Commands.ReplaceMenuItemBundle;

public record ReplaceMenuItemBundleCommand(
    Guid MenuItemId,
    bool IsBundle,
    decimal? BundlePrice,
    IReadOnlyCollection<UpsertBundleComponentInput> Components) : IRequest<AdminMenuItemBundleDto>;

public class ReplaceMenuItemBundleCommandValidator : AbstractValidator<ReplaceMenuItemBundleCommand>
{
    public ReplaceMenuItemBundleCommandValidator()
    {
        RuleFor(c => c.MenuItemId).NotEmpty();
        RuleFor(c => c.BundlePrice)
            .GreaterThan(0)
            .When(c => c.BundlePrice.HasValue)
            .WithMessage("قیمت باندل باید بزرگ‌تر از صفر باشد.");

        When(c => c.IsBundle, () =>
        {
            RuleFor(c => c.Components)
                .NotEmpty()
                .WithMessage("برای محصول باندل حداقل یک آیتم زیرمجموعه انتخاب کنید.");
            RuleForEach(c => c.Components).ChildRules(item =>
            {
                item.RuleFor(i => i.ComponentMenuItemId).NotEmpty();
                item.RuleFor(i => i.Quantity)
                    .InclusiveBetween(1, MenuItemBundleItem.MaxQuantityPerComponent)
                    .WithMessage($"تعداد هر آیتم در باندل باید بین ۱ تا {MenuItemBundleItem.MaxQuantityPerComponent} باشد.");
                item.RuleFor(i => i.DisplayOrder).GreaterThanOrEqualTo(0);
            });
        });
    }
}

public class ReplaceMenuItemBundleCommandHandler : IRequestHandler<ReplaceMenuItemBundleCommand, AdminMenuItemBundleDto>
{
    private readonly IApplicationDbContext _dbContext;

    public ReplaceMenuItemBundleCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AdminMenuItemBundleDto> Handle(
        ReplaceMenuItemBundleCommand request,
        CancellationToken cancellationToken)
    {
        var menuItem = await _dbContext.MenuItems
            .Include(m => m.RecipeItems)
            .ThenInclude(r => r.Ingredient)
            .Include(m => m.BundleItems)
            .FirstOrDefaultAsync(m => m.Id == request.MenuItemId, cancellationToken)
            ?? throw new KeyNotFoundException("آیتم منو یافت نشد.");

        if (!request.IsBundle)
        {
            if (menuItem.BundleItems.Count > 0)
            {
                _dbContext.MenuItemBundleItems.RemoveRange(menuItem.BundleItems);
                menuItem.BundleItems.Clear();
            }

            menuItem.SetIsBundle(false);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return new AdminMenuItemBundleDto(
                menuItem.Id,
                menuItem.PersianName,
                menuItem.EnglishName,
                false,
                BundlePricingService.ResolveEffectiveSellingPrice(menuItem),
                0m,
                0m,
                0,
                menuItem.RecipeItems.Count > 0
                    ? Math.Round(Services.MenuPricingService.ComputeCostPerServing(menuItem), 2)
                    : 0m,
                0m,
                null,
                false,
                []);
        }

        // Prevent converting an item that is already a component of another bundle into a bundle (no nested bundles).
        var usedInParentBundle = await _dbContext.MenuItemBundleItems
            .AsNoTracking()
            .Where(b => b.ComponentMenuItemId == menuItem.Id && b.BundleMenuItemId != menuItem.Id)
            .Select(b => b.BundleMenuItem.PersianName)
            .FirstOrDefaultAsync(cancellationToken);

        if (usedInParentBundle is not null)
        {
            throw new DomainException(
                $"آیتم «{menuItem.PersianName}» هم‌اکنون جزو باندل «{usedInParentBundle}» است و نمی‌تواند خودش به باندل تبدیل شود.");
        }

        var inputs = (request.Components ?? []).ToList();
        if (inputs.Count == 0)
        {
            throw new DomainException("برای محصول باندل حداقل یک آیتم زیرمجموعه انتخاب کنید.");
        }

        if (inputs.Any(i => i.ComponentMenuItemId == menuItem.Id))
        {
            throw new DomainException("یک باندل نمی‌تواند شامل خودش باشد.");
        }

        // Check for duplicate (ComponentMenuItemId, SizeKey) pairs
        var seenPairs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var input in inputs)
        {
            var normalizedSize = MenuItemBundleItem.NormalizeSizeKey(input.SizeKey) ?? string.Empty;
            var key = $"{input.ComponentMenuItemId}:{normalizedSize}";
            if (!seenPairs.Add(key))
            {
                throw new DomainException("آیتم تکراری با اندازه یکسان در باندل وجود دارد؛ به‌جای تکرار، تعداد آن را افزایش دهید.");
            }
        }

        var componentIds = inputs.Select(i => i.ComponentMenuItemId).Distinct().ToList();
        var componentsById = await _dbContext.MenuItems
            .AsNoTracking()
            .Include(m => m.RecipeItems)
            .ThenInclude(r => r.Ingredient)
            .Where(m => componentIds.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id, cancellationToken);

        if (componentsById.Count != componentIds.Count)
        {
            throw new KeyNotFoundException("یکی از آیتم‌های انتخاب‌شده برای باندل در منو پیدا نشد.");
        }

        var nestedBundle = componentsById.Values.FirstOrDefault(c => c.IsBundle);
        if (nestedBundle is not null)
        {
            throw new DomainException(
                $"آیتم «{nestedBundle.PersianName}» خودش یک باندل است. باندل تودرتو مجاز نیست.");
        }

        // Replace bundle lines atomically
        if (menuItem.BundleItems.Count > 0)
        {
            _dbContext.MenuItemBundleItems.RemoveRange(menuItem.BundleItems);
            menuItem.BundleItems.Clear();
        }

        var createdEntities = new List<MenuItemBundleItem>(inputs.Count);
        for (var index = 0; index < inputs.Count; index++)
        {
            var input = inputs[index];
            var normalizedSize = MenuItemBundleItem.NormalizeSizeKey(input.SizeKey);
            var comp = componentsById[input.ComponentMenuItemId];

            // Verify requested size actually exists on component if explicitly specified
            if (normalizedSize == "small" && !comp.SmallPrice.HasValue ||
                normalizedSize == "medium" && !comp.MediumPrice.HasValue ||
                normalizedSize == "large" && !comp.LargePrice.HasValue)
            {
                throw new DomainException($"اندازه انتخاب‌شده برای «{comp.PersianName}» تعریف نشده است.");
            }

            var entity = new MenuItemBundleItem(
                menuItem.Id,
                input.ComponentMenuItemId,
                input.Quantity,
                normalizedSize,
                input.DisplayOrder >= 0 ? input.DisplayOrder : index);

            _dbContext.MenuItemBundleItems.Add(entity);
            createdEntities.Add(entity);
        }

        menuItem.SetIsBundle(true);

        // Optionally update the bundle's selling price directly from the bundle studio.
        if (request.BundlePrice is { } newBundlePrice && newBundlePrice > 0m)
        {
            menuItem.Update(
                menuItem.CategoryId,
                menuItem.PersianName,
                menuItem.EnglishName,
                menuItem.Description,
                menuItem.ImageUrl,
                menuItem.GalleryImageUrls,
                specialOfferPrice: null,
                menuItem.TaxPercent,
                isOrderable: true,
                menuItem.PreparingDurationMinutes,
                menuItem.Calories,
                menuItem.Allergens,
                menuItem.Volume,
                smallPrice: null,
                smallSpecialOfferPrice: null,
                smallVolume: null,
                smallName: null,
                mediumPrice: null,
                mediumSpecialOfferPrice: null,
                mediumVolume: null,
                mediumName: null,
                largePrice: null,
                largeSpecialOfferPrice: null,
                largeVolume: null,
                largeName: null,
                menuItem.Tags,
                menuItem.Badge,
                menuItem.Featured,
                menuItem.Accent,
                menuItem.ShortLabel,
                menuItem.DisplayOrder,
                menuItem.IsActive,
                price: newBundlePrice);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Attach navigation references for immediate valuation return
        var reloaded = await _dbContext.MenuItems
            .AsNoTracking()
            .Include(m => m.RecipeItems)
            .ThenInclude(r => r.Ingredient)
            .Include(m => m.BundleItems)
            .ThenInclude(b => b.ComponentMenuItem)
            .ThenInclude(c => c.RecipeItems)
            .ThenInclude(r => r.Ingredient)
            .FirstAsync(m => m.Id == menuItem.Id, cancellationToken);

        var orderedItems = reloaded.BundleItems
            .OrderBy(b => b.DisplayOrder)
            .ThenBy(b => b.Id)
            .ToList();

        var valuation = BundlePricingService.Evaluate(reloaded, orderedItems);

        return new AdminMenuItemBundleDto(
            reloaded.Id,
            reloaded.PersianName,
            reloaded.EnglishName,
            reloaded.IsBundle,
            valuation.BundlePrice,
            valuation.RegularTotal,
            valuation.SavingsAmount,
            valuation.SavingsPercent,
            valuation.TotalCostPerServing,
            valuation.EstimatedProfit,
            valuation.EstimatedMarginPercent,
            valuation.AllComponentsAvailable,
            orderedItems.Zip(valuation.Components, (entity, info) => new AdminBundleComponentDto(
                entity.Id,
                info.ComponentMenuItemId,
                info.PersianName,
                info.EnglishName,
                info.ImageUrl,
                info.SizeKey,
                info.SizeLabel,
                info.Quantity,
                info.UnitRegularPrice,
                info.LineRegularPrice,
                info.UnitCost,
                info.LineCost,
                info.IsAvailable,
                info.DisplayOrder)).ToArray());
    }
}
