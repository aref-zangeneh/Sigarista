using System.Text.Json;
using Sugarista.Application.MenuItems.Services;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.MenuItems.Bundles;

public sealed record BundleComponentPriceInfo(
    Guid ComponentMenuItemId,
    string PersianName,
    string EnglishName,
    string? ImageUrl,
    string Accent,
    string ShortLabel,
    string? SizeKey,
    string? SizeLabel,
    int Quantity,
    decimal UnitRegularPrice,
    decimal LineRegularPrice,
    decimal UnitCost,
    decimal LineCost,
    bool IsAvailable,
    int DisplayOrder);

public sealed record BundleValuationSummary(
    decimal RegularTotal,
    decimal BundlePrice,
    decimal SavingsAmount,
    int SavingsPercent,
    decimal TotalCostPerServing,
    decimal EstimatedProfit,
    decimal? EstimatedMarginPercent,
    bool AllComponentsAvailable,
    IReadOnlyList<BundleComponentPriceInfo> Components);

public sealed record OrderLineBundleComponentSnapshot(
    Guid ComponentMenuItemId,
    string PersianName,
    string EnglishName,
    string? SizeKey,
    string? SizeLabel,
    int Quantity,
    decimal UnitRegularPrice,
    decimal LineRegularPrice);

public sealed record OrderLineBundleSnapshot(
    decimal RegularTotal,
    decimal BundleUnitPrice,
    decimal SavingsAmount,
    int SavingsPercent,
    IReadOnlyList<OrderLineBundleComponentSnapshot> Components);

public static class BundlePricingService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Resolves the real standalone selling price of a component item (for a specific size
    /// if provided, otherwise its default variant/price). Uses the real price a customer
    /// would pay when buying the item individually.
    /// </summary>
    public static (string? SizeKey, string? SizeLabel, decimal UnitPrice) ResolveComponentUnitPrice(
        MenuItem component,
        string? requestedSizeKey)
    {
        var normalized = string.IsNullOrWhiteSpace(requestedSizeKey)
            ? null
            : requestedSizeKey.Trim().ToLowerInvariant();

        if (normalized == "small" && component.SmallPrice.HasValue)
        {
            return ("small", component.SmallName ?? "کوچک", component.SmallSpecialOfferPrice ?? component.SmallPrice.Value);
        }

        if (normalized == "medium" && component.MediumPrice.HasValue)
        {
            return ("medium", component.MediumName ?? "متوسط", component.MediumSpecialOfferPrice ?? component.MediumPrice.Value);
        }

        if (normalized == "large" && component.LargePrice.HasValue)
        {
            return ("large", component.LargeName ?? "بزرگ", component.LargeSpecialOfferPrice ?? component.LargePrice.Value);
        }

        if (component.MediumPrice.HasValue)
        {
            return ("medium", component.MediumName ?? "متوسط", component.MediumSpecialOfferPrice ?? component.MediumPrice.Value);
        }

        if (component.SmallPrice.HasValue)
        {
            return ("small", component.SmallName ?? "کوچک", component.SmallSpecialOfferPrice ?? component.SmallPrice.Value);
        }

        if (component.LargePrice.HasValue)
        {
            return ("large", component.LargeName ?? "بزرگ", component.LargeSpecialOfferPrice ?? component.LargePrice.Value);
        }

        return (null, null, component.SpecialOfferPrice ?? component.Price);
    }

    public static decimal ResolveEffectiveSellingPrice(MenuItem item)
    {
        var (_, _, price) = ResolveComponentUnitPrice(item, null);
        return price;
    }

    /// <summary>
    /// Computes the full live financial & customer-facing valuation of a bundle from
    /// its component items' real current standalone prices and recipe costs.
    /// </summary>
    public static BundleValuationSummary Evaluate(
        MenuItem bundle,
        IReadOnlyCollection<MenuItemBundleItem> bundleItems,
        decimal? overrideBundleSellingPrice = null)
    {
        var ordered = bundleItems
            .OrderBy(entry => entry.DisplayOrder)
            .ThenBy(entry => entry.Id)
            .ToList();

        var components = new List<BundleComponentPriceInfo>(ordered.Count);
        decimal regularTotal = 0m;
        decimal componentsCost = 0m;
        var allAvailable = ordered.Count > 0;

        foreach (var entry in ordered)
        {
            if (entry.ComponentMenuItem is null)
            {
                allAvailable = false;
                continue;
            }

            var comp = entry.ComponentMenuItem;
            var (sizeKey, sizeLabel, unitPrice) = ResolveComponentUnitPrice(comp, entry.SizeKey);
            var linePrice = unitPrice * entry.Quantity;
            var unitCost = ComputeComponentUnitCost(comp, sizeKey);
            var lineCost = unitCost * entry.Quantity;
            var isAvailable = comp.IsActive && comp.IsOrderable && unitPrice > 0m;

            if (!isAvailable)
            {
                allAvailable = false;
            }

            regularTotal += linePrice;
            componentsCost += lineCost;

            components.Add(new BundleComponentPriceInfo(
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
                Math.Round(unitCost, 2),
                Math.Round(lineCost, 2),
                isAvailable,
                entry.DisplayOrder));
        }

        // Include any direct packaging/recipe items attached to the bundle itself (e.g., gift box).
        var ownRecipeCost = bundle.RecipeItems.Count > 0
            ? MenuPricingService.ComputeCostPerServing(bundle)
            : 0m;

        var totalCost = Math.Round(componentsCost + ownRecipeCost, 2);
        var bundlePrice = overrideBundleSellingPrice ?? ResolveEffectiveSellingPrice(bundle);
        var savingsAmount = regularTotal > bundlePrice ? regularTotal - bundlePrice : 0m;
        var savingsPercent = regularTotal > 0m && savingsAmount > 0m
            ? (int)Math.Round((savingsAmount / regularTotal) * 100m, MidpointRounding.AwayFromZero)
            : 0;

        var estimatedProfit = Math.Round(bundlePrice - totalCost, 2);
        decimal? estimatedMargin = bundlePrice > 0m
            ? Math.Round((estimatedProfit / bundlePrice) * 100m, 1)
            : null;

        return new BundleValuationSummary(
            RegularTotal: regularTotal,
            BundlePrice: bundlePrice,
            SavingsAmount: savingsAmount,
            SavingsPercent: savingsPercent,
            TotalCostPerServing: totalCost,
            EstimatedProfit: estimatedProfit,
            EstimatedMarginPercent: estimatedMargin,
            AllComponentsAvailable: allAvailable,
            Components: components);
    }

    public static decimal ComputeComponentUnitCost(MenuItem component, string? sizeKey)
    {
        if (component.RecipeItems.Count == 0)
        {
            return 0m;
        }

        var baseCost = MenuPricingService.ComputeCostPerServing(component);
        if (string.IsNullOrWhiteSpace(sizeKey))
        {
            return baseCost;
        }

        var factors = MenuPricingService.ComputeSizeFactors(component);
        var factor = factors.TryGetValue(sizeKey.Trim().ToLowerInvariant(), out var resolved) ? resolved : 1m;
        return baseCost * factor;
    }

    public static string SerializeSnapshot(BundleValuationSummary valuation)
    {
        var snapshot = new OrderLineBundleSnapshot(
            valuation.RegularTotal,
            valuation.BundlePrice,
            valuation.SavingsAmount,
            valuation.SavingsPercent,
            valuation.Components.Select(c => new OrderLineBundleComponentSnapshot(
                c.ComponentMenuItemId,
                c.PersianName,
                c.EnglishName,
                c.SizeKey,
                c.SizeLabel,
                c.Quantity,
                c.UnitRegularPrice,
                c.LineRegularPrice)).ToList());

        return JsonSerializer.Serialize(snapshot, JsonOptions);
    }

    public static OrderLineBundleSnapshot? DeserializeSnapshot(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<OrderLineBundleSnapshot>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }
}
