using Sugarista.Domain.Entities;

namespace Sugarista.Application.Orders;

internal static class OrderPricing
{
    internal record ResolvedLine(
        Guid MenuItemId,
        string PersianName,
        string EnglishName,
        string? SizeKey,
        string? SizeLabel,
        int Quantity,
        decimal UnitPrice,
        IReadOnlyCollection<string> ModifierLabels);

    internal static ResolvedLine ResolveLine(
        MenuItem menuItem,
        string? sizeKey,
        int quantity,
        IReadOnlyDictionary<Guid, MenuModifier> modifiersById,
        IReadOnlyCollection<Guid> modifierIds)
    {
        if (!menuItem.IsOrderable || !menuItem.IsActive)
        {
            throw new InvalidOperationException($"'{menuItem.PersianName}' is not available for ordering.");
        }

        var (resolvedSizeKey, sizeLabel, unitPrice) = ResolveUnitPrice(menuItem, sizeKey);
        var modifierLabels = modifierIds
            .Where(modifiersById.ContainsKey)
            .Select(id => modifiersById[id].PersianName)
            .ToArray();

        return new ResolvedLine(
            menuItem.Id,
            menuItem.PersianName,
            menuItem.EnglishName,
            resolvedSizeKey,
            sizeLabel,
            quantity,
            unitPrice,
            modifierLabels);
    }

    private static (string? SizeKey, string? SizeLabel, decimal UnitPrice) ResolveUnitPrice(MenuItem menuItem, string? sizeKey)
    {
        var normalizedKey = string.IsNullOrWhiteSpace(sizeKey)
            ? null
            : sizeKey.Trim().ToLowerInvariant();

        if (normalizedKey is "small")
        {
            return ResolveVariant("small", "کوچک", menuItem.SmallPrice, menuItem.SmallSpecialOfferPrice, menuItem);
        }

        if (normalizedKey is "medium")
        {
            return ResolveVariant("medium", "متوسط", menuItem.MediumPrice, menuItem.MediumSpecialOfferPrice, menuItem);
        }

        if (normalizedKey is "large")
        {
            return ResolveVariant("large", "بزرگ", menuItem.LargePrice, menuItem.LargeSpecialOfferPrice, menuItem);
        }

        if (menuItem.MediumPrice.HasValue)
        {
            return ResolveVariant("medium", "متوسط", menuItem.MediumPrice, menuItem.MediumSpecialOfferPrice, menuItem);
        }

        if (menuItem.SmallPrice.HasValue)
        {
            return ResolveVariant("small", "کوچک", menuItem.SmallPrice, menuItem.SmallSpecialOfferPrice, menuItem);
        }

        if (menuItem.LargePrice.HasValue)
        {
            return ResolveVariant("large", "بزرگ", menuItem.LargePrice, menuItem.LargeSpecialOfferPrice, menuItem);
        }

        var fallbackPrice = menuItem.SpecialOfferPrice ?? menuItem.Price;
        if (fallbackPrice <= 0)
        {
            throw new InvalidOperationException($"'{menuItem.PersianName}' has no valid price.");
        }

        return (null, null, fallbackPrice);
    }

    private static (string? SizeKey, string? SizeLabel, decimal UnitPrice) ResolveVariant(
        string key,
        string label,
        decimal? price,
        decimal? specialOfferPrice,
        MenuItem menuItem)
    {
        if (!price.HasValue)
        {
            throw new InvalidOperationException($"Size '{label}' is not available for '{menuItem.PersianName}'.");
        }

        return (key, label, specialOfferPrice ?? price.Value);
    }
}
