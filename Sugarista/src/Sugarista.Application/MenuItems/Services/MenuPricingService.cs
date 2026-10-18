using Microsoft.EntityFrameworkCore;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.MenuItems.Services;

/// <summary>
/// Computes the material (+packaging) cost of one serving of a menu item
/// from its recipe lines using each ingredient's last known unit cost.
/// </summary>
public static class MenuPricingService
{
    public static decimal ComputeCostPerServing(MenuItem menuItem)
    {
        return menuItem.RecipeItems
            .Sum(line => line.Ingredient.UnitCost * line.Quantity);
    }

    /// <summary>
    /// Extracts a numeric volume (ml) out of free-form volume labels like "۲۰۰ ml" / "200".
    /// </summary>
    public static decimal? ParseVolumeMl(string? volume)
    {
        if (string.IsNullOrWhiteSpace(volume))
        {
            return null;
        }

        var digits = new string(volume.Trim().Where(char.IsDigit).ToArray());
        return decimal.TryParse(digits, out var parsed) && parsed > 0 ? parsed : null;
    }

    public static IReadOnlyDictionary<string, decimal> ComputeSizeFactors(MenuItem menuItem)
    {
        var factors = new Dictionary<string, decimal>();

        var baseVolume =
            ParseVolumeMl(menuItem.MediumVolume)
            ?? ParseVolumeMl(menuItem.SmallVolume)
            ?? ParseVolumeMl(menuItem.LargeVolume);

        factors["small"] = ResolveFactor(menuItem.SmallVolume, baseVolume);
        factors["medium"] = ResolveFactor(menuItem.MediumVolume, baseVolume);
        factors["large"] = ResolveFactor(menuItem.LargeVolume, baseVolume);

        return factors;
    }

    private static decimal ResolveFactor(string? volume, decimal? baseVolume)
    {
        var parsed = ParseVolumeMl(volume);
        if (parsed is not { } sizeMl || baseVolume is not { } baseMl || baseMl <= 0)
        {
            return 1m;
        }

        return sizeMl / baseMl;
    }

    /// <summary>Final price rounding: Iranian Toman granularity of 1,000.</summary>
    public static decimal RoundFinalPrice(decimal value)
    {
        if (value <= 0)
        {
            return 0m;
        }

        return Math.Ceiling(value / 1000m) * 1000m;
    }
}
