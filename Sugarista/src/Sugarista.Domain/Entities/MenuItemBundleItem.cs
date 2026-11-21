using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

/// <summary>
/// One component item inside a bundle <see cref="MenuItem"/> (where <see cref="MenuItem.IsBundle"/> is true).
/// References the real standalone <see cref="ComponentMenuItem"/> so its real price, recipe,
/// finished stock, and availability always stay in sync.
/// </summary>
public class MenuItemBundleItem : BaseEntity
{
    public const int MaxQuantityPerComponent = 50;

    private static readonly HashSet<string> AllowedSizeKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "small",
        "medium",
        "large"
    };

    private MenuItemBundleItem()
    {
    }

    public MenuItemBundleItem(
        Guid bundleMenuItemId,
        Guid componentMenuItemId,
        int quantity = 1,
        string? sizeKey = null,
        int displayOrder = 0)
    {
        Update(bundleMenuItemId, componentMenuItemId, quantity, sizeKey, displayOrder);
    }

    public Guid BundleMenuItemId { get; private set; }

    public MenuItem BundleMenuItem { get; private set; } = null!;

    public Guid ComponentMenuItemId { get; private set; }

    public MenuItem ComponentMenuItem { get; private set; } = null!;

    /// <summary>
    /// Optional size key ("small", "medium", "large") when the component item has size variants.
    /// Null resolves to the component's default price (medium -> small -> large -> base price).
    /// </summary>
    public string? SizeKey { get; private set; }

    public int Quantity { get; private set; } = 1;

    public int DisplayOrder { get; private set; }

    public void Update(
        Guid bundleMenuItemId,
        Guid componentMenuItemId,
        int quantity,
        string? sizeKey,
        int displayOrder)
    {
        if (bundleMenuItemId == Guid.Empty)
        {
            throw new DomainException("شناسه محصول باندل الزامی است.");
        }

        if (componentMenuItemId == Guid.Empty)
        {
            throw new DomainException("انتخاب آیتم زیرمجموعه باندل الزامی است.");
        }

        if (bundleMenuItemId == componentMenuItemId)
        {
            throw new DomainException("یک باندل نمی‌تواند شامل خودش باشد.");
        }

        if (quantity < 1 || quantity > MaxQuantityPerComponent)
        {
            throw new DomainException($"تعداد هر آیتم در باندل باید بین ۱ تا {MaxQuantityPerComponent} باشد.");
        }

        if (displayOrder < 0)
        {
            throw new DomainException("ترتیب نمایش آیتم باندل نمی‌تواند منفی باشد.");
        }

        var normalizedSizeKey = NormalizeSizeKey(sizeKey);

        BundleMenuItemId = bundleMenuItemId;
        ComponentMenuItemId = componentMenuItemId;
        Quantity = quantity;
        SizeKey = normalizedSizeKey;
        DisplayOrder = displayOrder;
    }

    public static string? NormalizeSizeKey(string? sizeKey)
    {
        if (string.IsNullOrWhiteSpace(sizeKey))
        {
            return null;
        }

        var trimmed = sizeKey.Trim().ToLowerInvariant();
        if (!AllowedSizeKeys.Contains(trimmed))
        {
            throw new DomainException("اندازه انتخاب‌شده برای آیتم باندل معتبر نیست (small, medium, large).");
        }

        return trimmed;
    }
}
