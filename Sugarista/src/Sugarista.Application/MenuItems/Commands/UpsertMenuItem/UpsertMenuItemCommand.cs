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
    decimal? Price = null,
    decimal? PurchasePrice = null,
    bool TrackInventory = false,
    decimal? StockQuantity = null,
    decimal? MinStockAlert = null,
    string? PriceChangeNote = null) : IRequest<Guid>;

public class UpsertMenuItemCommandValidator : AbstractValidator<UpsertMenuItemCommand>
{
    public UpsertMenuItemCommandValidator()
    {
        RuleFor(command => command.CategoryId)
            .NotEmpty()
            .WithMessage("انتخاب دسته‌بندی الزامی است.");
        RuleFor(command => command.PersianName)
            .NotEmpty()
            .WithMessage("نام فارسی آیتم الزامی است.")
            .MaximumLength(200);
        // Everything besides the name is optional at definition time: an item
        // can be drafted with just a name (and a category) and be completed later.
        RuleFor(command => command.EnglishName).MaximumLength(200);
        RuleFor(command => command.Description).MaximumLength(1000);
        RuleFor(command => command.ImageUrl).MaximumLength(1000);
        RuleFor(command => command.SpecialOfferPrice).GreaterThanOrEqualTo(0);
        RuleFor(command => command.TaxPercent).GreaterThanOrEqualTo(0);
        RuleFor(command => command.PreparingDurationMinutes).GreaterThanOrEqualTo(0);
        RuleFor(command => command.Calories).GreaterThanOrEqualTo(0);
        RuleFor(command => command.Volume).MaximumLength(100);
        RuleFor(command => command.SmallPrice).GreaterThanOrEqualTo(0);
        RuleFor(command => command.SmallSpecialOfferPrice).GreaterThanOrEqualTo(0);
        RuleFor(command => command.SmallVolume).MaximumLength(100);
        RuleFor(command => command.SmallName).MaximumLength(60);
        RuleFor(command => command.MediumPrice).GreaterThanOrEqualTo(0);
        RuleFor(command => command.MediumSpecialOfferPrice).GreaterThanOrEqualTo(0);
        RuleFor(command => command.MediumVolume).MaximumLength(100);
        RuleFor(command => command.MediumName).MaximumLength(60);
        RuleFor(command => command.LargePrice).GreaterThanOrEqualTo(0);
        RuleFor(command => command.LargeSpecialOfferPrice).GreaterThanOrEqualTo(0);
        RuleFor(command => command.LargeVolume).MaximumLength(100);
        RuleFor(command => command.LargeName).MaximumLength(60);
        RuleFor(command => command.Price).GreaterThanOrEqualTo(0);
        RuleFor(command => command.PurchasePrice)
            .GreaterThanOrEqualTo(0)
            .WithMessage("قیمت خرید نمی‌تواند منفی باشد.");
        RuleFor(command => command.StockQuantity)
            .GreaterThanOrEqualTo(0)
            .WithMessage("تعداد موجودی نمی‌تواند منفی باشد.");
        RuleFor(command => command.MinStockAlert)
            .GreaterThanOrEqualTo(0)
            .WithMessage("حداقل موجودی هشدار نمی‌تواند منفی باشد.");
        RuleFor(command => command.PriceChangeNote).MaximumLength(500);
        // No price rule anymore: an item without any price is simply saved
        // unpriced and the domain turns ordering off for it automatically.
        RuleFor(command => command.Accent).MaximumLength(200);
        RuleFor(command => command.ShortLabel).MaximumLength(10);
        RuleFor(command => command.DisplayOrder).GreaterThanOrEqualTo(0);
    }
}

public class UpsertMenuItemCommandHandler : IRequestHandler<UpsertMenuItemCommand, Guid>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IImageFileStore _imageStore;
    private readonly ICurrentUserService _currentUserService;

    public UpsertMenuItemCommandHandler(
        IApplicationDbContext dbContext,
        IImageFileStore imageStore,
        ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _imageStore = imageStore;
        _currentUserService = currentUserService;
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
        var changedBy = await ResolveActorNameAsync(_currentUserService.UserId, cancellationToken);

        if (request.Id is { } itemId)
        {
            var existingItem = await _dbContext.MenuItems
                .FirstOrDefaultAsync(item => item.Id == itemId, cancellationToken)
                ?? throw new KeyNotFoundException("Menu item was not found.");

            var previousPurchasePrice = existingItem.PurchasePrice;
            var previousSalePrice = existingItem.Price;

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
                request.SmallName,
                request.MediumPrice,
                request.MediumSpecialOfferPrice,
                request.MediumVolume,
                request.MediumName,
                request.LargePrice,
                request.LargeSpecialOfferPrice,
                request.LargeVolume,
                request.LargeName,
                request.Tags,
                request.Badge,
                request.Featured,
                request.Accent,
                request.ShortLabel,
                request.DisplayOrder,
                request.IsActive,
                request.Price);

            existingItem.SetPurchasePrice(request.PurchasePrice);

            await SyncInventoryStockAsync(
                existingItem.Id,
                request.TrackInventory,
                request.StockQuantity,
                request.MinStockAlert,
                cancellationToken);

            var purchaseChanged = previousPurchasePrice != existingItem.PurchasePrice;
            var saleChanged = previousSalePrice != existingItem.Price;
            if (purchaseChanged || saleChanged)
            {
                _dbContext.MenuItemPriceHistories.Add(
                    MenuItemPriceHistory.Create(
                        existingItem.Id,
                        previousPurchasePrice,
                        existingItem.PurchasePrice,
                        previousSalePrice,
                        existingItem.Price,
                        MenuItemPriceHistory.SourceManualEdit,
                        request.PriceChangeNote,
                        changedBy));
            }
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
                request.SmallName,
                request.MediumPrice,
                request.MediumSpecialOfferPrice,
                request.MediumVolume,
                request.MediumName,
                request.LargePrice,
                request.LargeSpecialOfferPrice,
                request.LargeVolume,
                request.LargeName,
                request.Tags,
                request.Badge,
                request.Featured,
                request.Accent,
                request.ShortLabel,
                request.DisplayOrder,
                request.IsActive,
                request.Price);

            menuItem.SetPurchasePrice(request.PurchasePrice);

            _dbContext.MenuItems.Add(menuItem);

            if (request.TrackInventory)
            {
                var stock = new MenuItemStock(menuItem.Id, request.MinStockAlert ?? 0m);
                stock.SetStock(request.StockQuantity ?? 0m, request.MinStockAlert ?? 0m);
                _dbContext.MenuItemStocks.Add(stock);
            }

            if (menuItem.PurchasePrice.HasValue || menuItem.Price > 0m)
            {
                _dbContext.MenuItemPriceHistories.Add(
                    MenuItemPriceHistory.Create(
                        menuItem.Id,
                        null,
                        menuItem.PurchasePrice,
                        null,
                        menuItem.Price,
                        MenuItemPriceHistory.SourceInitial,
                        request.PriceChangeNote,
                        changedBy));
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            return menuItem.Id;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Images are immutable files (fresh GUID every upload); any URL that
        // was dropped from the item would become an orphan, so remove it —
        // UNLESS another menu item (e.g. a duplicated item) still references it.
        if (replacedImageUrls.Count > 0)
        {
            var otherItemImages = await _dbContext.MenuItems
                .AsNoTracking()
                .Where(item => item.Id != request.Id!.Value)
                .Select(item => new { item.ImageUrl, item.GalleryImageUrls })
                .ToListAsync(cancellationToken);

            var stillReferencedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var other in otherItemImages)
            {
                if (!string.IsNullOrWhiteSpace(other.ImageUrl))
                {
                    stillReferencedKeys.Add(CanonicalImageKey(other.ImageUrl));
                }

                foreach (var galleryUrl in other.GalleryImageUrls)
                {
                    if (!string.IsNullOrWhiteSpace(galleryUrl))
                    {
                        stillReferencedKeys.Add(CanonicalImageKey(galleryUrl));
                    }
                }
            }

            var safeToDelete = replacedImageUrls
                .Where(url => !stillReferencedKeys.Contains(CanonicalImageKey(url)))
                .ToList();

            if (safeToDelete.Count > 0)
            {
                try
                {
                    await _imageStore.DeleteImageAsync(safeToDelete, cancellationToken);
                }
                catch (IOException)
                {
                    // Disk housekeeping must never fail the business operation.
                }
            }
        }

        return request.Id!.Value;
    }

    private async Task<string?> ResolveActorNameAsync(string? userId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        if (Guid.TryParse(userId, out var adminId))
        {
            var admin = await _dbContext.AdminUsers
                .AsNoTracking()
                .Where(u => u.Id == adminId)
                .Select(u => new { u.DisplayName, u.Username })
                .FirstOrDefaultAsync(cancellationToken);

            if (admin is not null)
            {
                return string.IsNullOrWhiteSpace(admin.DisplayName) ? admin.Username : admin.DisplayName;
            }
        }

        return userId;
    }

    private async Task SyncInventoryStockAsync(
        Guid menuItemId,
        bool trackInventory,
        decimal? stockQuantity,
        decimal? minStockAlert,
        CancellationToken cancellationToken)
    {
        var existingStock = await _dbContext.MenuItemStocks
            .FirstOrDefaultAsync(stock => stock.MenuItemId == menuItemId, cancellationToken);

        if (trackInventory)
        {
            if (existingStock is null)
            {
                existingStock = new MenuItemStock(menuItemId, minStockAlert ?? 0m);
                _dbContext.MenuItemStocks.Add(existingStock);
            }

            existingStock.SetStock(
                stockQuantity ?? existingStock.CurrentQuantity,
                minStockAlert ?? existingStock.MinAlertQuantity);
        }
        else if (existingStock is not null)
        {
            _dbContext.MenuItemStocks.Remove(existingStock);
        }
    }

    internal static List<string> CollectReplacedImageUrls(
        string? previousImageUrl,
        IEnumerable<string> previousGallery,
        string? newImageUrl,
        IEnumerable<string> newGallery)
    {
        var kept = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(newImageUrl))
        {
            kept.Add(CanonicalImageKey(newImageUrl));
        }

        foreach (var url in newGallery)
        {
            if (!string.IsNullOrWhiteSpace(url))
            {
                kept.Add(CanonicalImageKey(url));
            }
        }

        var replaced = new List<string>();
        if (!string.IsNullOrWhiteSpace(previousImageUrl) && !kept.Contains(CanonicalImageKey(previousImageUrl)))
        {
            replaced.Add(previousImageUrl);
        }

        foreach (var url in previousGallery)
        {
            if (!string.IsNullOrWhiteSpace(url) && !kept.Contains(CanonicalImageKey(url)))
            {
                replaced.Add(url);
            }
        }

        return replaced;
    }

    private static string CanonicalImageKey(string url)
    {
        var trimmed = url.Trim();
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var absoluteUri) &&
            absoluteUri.AbsolutePath.StartsWith("/api/files/images/", StringComparison.OrdinalIgnoreCase))
        {
            return absoluteUri.AbsolutePath;
        }

        return trimmed;
    }
}
