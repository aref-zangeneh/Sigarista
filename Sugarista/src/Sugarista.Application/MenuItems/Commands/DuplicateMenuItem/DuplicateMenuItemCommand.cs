using Microsoft.EntityFrameworkCore;
using Sugarista.Application.MenuItems.Queries.GetAdminMenuItems;
using Sugarista.Domain.Entities;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Application.MenuItems.Commands.DuplicateMenuItem;

public record DuplicateMenuItemCommand(Guid Id) : IRequest<DuplicateMenuItemResultDto>;

public record DuplicateMenuItemResultDto(
    Guid Id,
    AdminMenuItemDto Item,
    int CopiedOptionGroupCount,
    int CopiedRecipeLineCount,
    int CopiedBundleItemCount);

public class DuplicateMenuItemCommandHandler
    : IRequestHandler<DuplicateMenuItemCommand, DuplicateMenuItemResultDto>
{
    private const string PersianCopySuffix = " (کپی)";
    private const string EnglishCopySuffix = " (Copy)";

    private readonly IApplicationDbContext _dbContext;

    public DuplicateMenuItemCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<DuplicateMenuItemResultDto> Handle(
        DuplicateMenuItemCommand request,
        CancellationToken cancellationToken)
    {
        var source = await _dbContext.MenuItems
            .AsNoTracking()
            .Include(item => item.Category)
            .Include(item => item.RecipeItems)
            .Include(item => item.BundleItems)
            .FirstOrDefaultAsync(item => item.Id == request.Id, cancellationToken)
            ?? throw new DomainException("آیتم منو برای کپی یافت نشد.");

        var sourceGroups = await _dbContext.MenuItemOptionGroups
            .AsNoTracking()
            .Include(group => group.Options)
            .ThenInclude(option => option.Ingredients)
            .Where(group => group.MenuItemId == source.Id)
            .OrderBy(group => group.DisplayOrder)
            .ToListAsync(cancellationToken);

        var hasSizes = source.SmallPrice.HasValue || source.MediumPrice.HasValue || source.LargePrice.HasValue;
        var basePrice = hasSizes ? (decimal?)null : source.Price;

        var copiedPersianName = BuildCopiedName(source.PersianName, PersianCopySuffix, 200);
        var copiedEnglishName = string.IsNullOrWhiteSpace(source.EnglishName)
            ? string.Empty
            : BuildCopiedName(source.EnglishName, EnglishCopySuffix, 200);

        var clone = new MenuItem(
            source.CategoryId,
            copiedPersianName,
            copiedEnglishName,
            source.Description,
            source.ImageUrl,
            source.GalleryImageUrls,
            source.SpecialOfferPrice,
            source.TaxPercent,
            source.IsOrderable,
            source.PreparingDurationMinutes,
            source.Calories,
            source.Allergens,
            source.Volume,
            source.SmallPrice,
            source.SmallSpecialOfferPrice,
            source.SmallVolume,
            source.SmallName,
            source.MediumPrice,
            source.MediumSpecialOfferPrice,
            source.MediumVolume,
            source.MediumName,
            source.LargePrice,
            source.LargeSpecialOfferPrice,
            source.LargeVolume,
            source.LargeName,
            source.Tags,
            source.Badge,
            source.Featured,
            source.Accent,
            source.ShortLabel,
            source.DisplayOrder + 1,
            source.IsActive,
            basePrice);

        clone.SetMarkupPercent(source.MarkupPercent);
        clone.UpdateRecipeDescription(source.RecipeDescription);
        clone.SetIsBundle(source.IsBundle);

        _dbContext.MenuItems.Add(clone);

        foreach (var recipe in source.RecipeItems)
        {
            _dbContext.RecipeItems.Add(
                new RecipeItem(
                    clone.Id,
                    recipe.IngredientId,
                    recipe.Quantity,
                    recipe.PreparationNote,
                    recipe.ShowToCustomer));
        }

        foreach (var bundleItem in source.BundleItems.OrderBy(b => b.DisplayOrder))
        {
            _dbContext.MenuItemBundleItems.Add(
                new MenuItemBundleItem(
                    clone.Id,
                    bundleItem.ComponentMenuItemId,
                    bundleItem.Quantity,
                    bundleItem.SizeKey,
                    bundleItem.DisplayOrder));
        }

        foreach (var sourceGroup in sourceGroups)
        {
            var clonedGroup = new MenuItemOptionGroup(
                clone.Id,
                sourceGroup.PersianName,
                sourceGroup.EnglishName,
                sourceGroup.IsRequired,
                sourceGroup.SelectionMode,
                sourceGroup.MinSelections,
                sourceGroup.MaxSelections,
                sourceGroup.DisplayOrder,
                sourceGroup.IsActive,
                sourceGroup.SourceTemplateId);

            foreach (var sourceOption in sourceGroup.Options.OrderBy(o => o.DisplayOrder))
            {
                var clonedOption = clonedGroup.AddOption(
                    sourceOption.PersianName,
                    sourceOption.EnglishName,
                    sourceOption.PriceDelta,
                    sourceOption.IsDefault,
                    sourceOption.IsActive,
                    sourceOption.DisplayOrder);

                foreach (var ingredientRow in sourceOption.Ingredients)
                {
                    clonedOption.Ingredients.Add(
                        new MenuItemOptionIngredient(
                            clonedOption.Id,
                            ingredientRow.IngredientId,
                            ingredientRow.Quantity));
                }
            }

            _dbContext.MenuItemOptionGroups.Add(clonedGroup);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var dto = new AdminMenuItemDto(
            clone.Id,
            clone.CategoryId,
            source.Category?.Name ?? string.Empty,
            source.Category?.Slug ?? string.Empty,
            clone.Name,
            clone.PersianName,
            clone.EnglishName,
            clone.Description,
            clone.Price,
            clone.ImageUrl,
            clone.GalleryImageUrls,
            clone.SpecialOfferPrice,
            clone.TaxPercent,
            clone.IsOrderable,
            clone.PreparingDurationMinutes,
            clone.Calories,
            clone.Allergens,
            clone.Volume,
            clone.SmallPrice,
            clone.SmallSpecialOfferPrice,
            clone.SmallVolume,
            clone.SmallName,
            clone.MediumPrice,
            clone.MediumSpecialOfferPrice,
            clone.MediumVolume,
            clone.MediumName,
            clone.LargePrice,
            clone.LargeSpecialOfferPrice,
            clone.LargeVolume,
            clone.LargeName,
            clone.Tags,
            clone.Badge,
            clone.Featured,
            clone.Accent,
            clone.ShortLabel,
            clone.DisplayOrder,
            clone.IsActive,
            clone.IsBundle,
            source.BundleItems.Count,
            null);

        return new DuplicateMenuItemResultDto(
            clone.Id,
            dto,
            sourceGroups.Count,
            source.RecipeItems.Count,
            source.BundleItems.Count);
    }

    private static string BuildCopiedName(string original, string suffix, int maxLength)
    {
        var trimmed = (original ?? string.Empty).Trim();
        if (trimmed.Length + suffix.Length <= maxLength)
        {
            return trimmed + suffix;
        }

        var available = Math.Max(1, maxLength - suffix.Length);
        return trimmed[..available].TrimEnd() + suffix;
    }
}
