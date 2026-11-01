using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.MenuItems.Commands.UpsertMenuItem;

public record UpsertMenuItemCommand(
    Guid? Id,
    Guid CategoryId,
    string PersianName,
    string EnglishName,
    string Description,
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
    bool IsActive) : IRequest<Guid>;

public class UpsertMenuItemCommandValidator : AbstractValidator<UpsertMenuItemCommand>
{
    public UpsertMenuItemCommandValidator()
    {
        RuleFor(command => command.CategoryId).NotEmpty();
        RuleFor(command => command.PersianName).NotEmpty().MaximumLength(200);
        RuleFor(command => command.EnglishName).NotEmpty().MaximumLength(200);
        RuleFor(command => command.Description).NotEmpty().MaximumLength(1000);
        RuleFor(command => command.ImageUrl).MaximumLength(1000);
        RuleFor(command => command.SpecialOfferPrice).GreaterThanOrEqualTo(0);
        RuleFor(command => command.TaxPercent).GreaterThanOrEqualTo(0);
        RuleFor(command => command.PreparingDurationMinutes).GreaterThanOrEqualTo(0);
        RuleFor(command => command.Calories).GreaterThanOrEqualTo(0);
        RuleFor(command => command.Volume).MaximumLength(100);
        RuleFor(command => command.SmallPrice).GreaterThanOrEqualTo(0);
        RuleFor(command => command.SmallSpecialOfferPrice).GreaterThanOrEqualTo(0);
        RuleFor(command => command.SmallVolume).MaximumLength(100);
        RuleFor(command => command.MediumPrice).GreaterThanOrEqualTo(0);
        RuleFor(command => command.MediumSpecialOfferPrice).GreaterThanOrEqualTo(0);
        RuleFor(command => command.MediumVolume).MaximumLength(100);
        RuleFor(command => command.LargePrice).GreaterThanOrEqualTo(0);
        RuleFor(command => command.LargeSpecialOfferPrice).GreaterThanOrEqualTo(0);
        RuleFor(command => command.LargeVolume).MaximumLength(100);
        RuleFor(command => command)
            .Must(command => command.SmallPrice.HasValue || command.MediumPrice.HasValue || command.LargePrice.HasValue)
            .WithMessage("At least one size price is required.");
        RuleFor(command => command.Accent).NotEmpty().MaximumLength(200);
        RuleFor(command => command.ShortLabel).NotEmpty().MaximumLength(10);
        RuleFor(command => command.DisplayOrder).GreaterThanOrEqualTo(0);
    }
}

public class UpsertMenuItemCommandHandler : IRequestHandler<UpsertMenuItemCommand, Guid>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IImageFileStore _imageStore;

    public UpsertMenuItemCommandHandler(IApplicationDbContext dbContext, IImageFileStore imageStore)
    {
        _dbContext = dbContext;
        _imageStore = imageStore;
    }

    public async Task<Guid> Handle(UpsertMenuItemCommand request, CancellationToken cancellationToken)
    {
        var categoryExists = await _dbContext.Categories
            .AnyAsync(category => category.Id == request.CategoryId, cancellationToken);

        if (!categoryExists)
        {
            throw new KeyNotFoundException("Selected category was not found.");
        }

        List<string> replacedImageUrls = [];

        if (request.Id is { } itemId)
        {
            var existingItem = await _dbContext.MenuItems
                .FirstOrDefaultAsync(item => item.Id == itemId, cancellationToken)
                ?? throw new KeyNotFoundException("Menu item was not found.");

            replacedImageUrls = CollectReplacedImageUrls(
                existingItem.ImageUrl,
                existingItem.GalleryImageUrls,
                request.ImageUrl,
                request.GalleryImageUrls);

            existingItem.Update(
                request.CategoryId,
                request.PersianName,
                request.EnglishName,
                request.Description,
                request.ImageUrl,
                request.GalleryImageUrls,
                request.SpecialOfferPrice,
                request.TaxPercent,
                request.IsOrderable,
                request.PreparingDurationMinutes,
                request.Calories,
                request.Allergens,
                request.Volume,
                request.SmallPrice,
                request.SmallSpecialOfferPrice,
                request.SmallVolume,
                request.MediumPrice,
                request.MediumSpecialOfferPrice,
                request.MediumVolume,
                request.LargePrice,
                request.LargeSpecialOfferPrice,
                request.LargeVolume,
                request.Tags,
                request.Badge,
                request.Featured,
                request.Accent,
                request.ShortLabel,
                request.DisplayOrder,
                request.IsActive);
        }
        else
        {
            var menuItem = new MenuItem(
                request.CategoryId,
                request.PersianName,
                request.EnglishName,
                request.Description,
                request.ImageUrl,
                request.GalleryImageUrls,
                request.SpecialOfferPrice,
                request.TaxPercent,
                request.IsOrderable,
                request.PreparingDurationMinutes,
                request.Calories,
                request.Allergens,
                request.Volume,
                request.SmallPrice,
                request.SmallSpecialOfferPrice,
                request.SmallVolume,
                request.MediumPrice,
                request.MediumSpecialOfferPrice,
                request.MediumVolume,
                request.LargePrice,
                request.LargeSpecialOfferPrice,
                request.LargeVolume,
                request.Tags,
                request.Badge,
                request.Featured,
                request.Accent,
                request.ShortLabel,
                request.DisplayOrder,
                request.IsActive);

            _dbContext.MenuItems.Add(menuItem);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return menuItem.Id;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Images are immutable files (fresh GUID every upload); any URL that
        // was dropped from the item would become an orphan, so remove it.
        if (replacedImageUrls.Count > 0)
        {
            try
            {
                await _imageStore.DeleteImageAsync(replacedImageUrls, cancellationToken);
            }
            catch (IOException)
            {
                // Disk housekeeping must never fail the business operation.
            }
        }

        return request.Id!.Value;
    }

    private static List<string> CollectReplacedImageUrls(
        string? previousImageUrl,
        IEnumerable<string> previousGallery,
        string? newImageUrl,
        IEnumerable<string> newGallery)
    {
        var kept = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(newImageUrl))
        {
            kept.Add(newImageUrl);
        }
        kept.UnionWith(newGallery.Where(url => !string.IsNullOrWhiteSpace(url)));

        var replaced = new List<string>();
        if (!string.IsNullOrWhiteSpace(previousImageUrl) && !kept.Contains(previousImageUrl))
        {
            replaced.Add(previousImageUrl);
        }
        foreach (var url in previousGallery)
        {
            if (!string.IsNullOrWhiteSpace(url) && !kept.Contains(url))
            {
                replaced.Add(url);
            }
        }

        return replaced;
    }
}
