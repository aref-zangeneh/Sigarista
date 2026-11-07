using System.Globalization;
using System.Text.Json;
using Sugarista.Domain.Entities;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Application.MenuItems.OptionGroups;

/// <summary>One validated per-item option chosen by the customer.</summary>
public sealed record MenuItemOptionSelectionRequest(Guid OptionId);

/// <summary>Exact ingredient usage of one option, frozen at order time.</summary>
public sealed record MenuItemOptionSnapshotIngredient(
    Guid IngredientId,
    string IngredientPersianName,
    string Unit,
    decimal Quantity);

/// <summary>Immutable snapshot of one option stored with the order line.</summary>
public sealed record MenuItemOptionSnapshotComponent(
    Guid GroupId,
    string GroupName,
    Guid OptionId,
    string OptionName,
    decimal PriceDelta);

/// <summary>
/// Immutable snapshot of the validated per-item options. It is serialised into
/// <see cref="OrderLine.OptionsJson"/> so the customer's choices and their
/// inventory effect never change even when prices or options are edited later.
/// </summary>
public sealed record MenuItemOptionSnapshot(
    IReadOnlyCollection<MenuItemOptionSnapshotComponent> Components,
    IReadOnlyCollection<MenuItemOptionSnapshotIngredient> Ingredients,
    decimal OptionsTotal,
    IReadOnlyCollection<string> Labels);

/// <summary>
/// Single source of truth for per-item option validation and pricing. The
/// backend always recomputes the price and enforces the required/optional
/// group rules; the client selection is never trusted for money.
/// </summary>
public static class MenuItemOptionPricing
{
    private static readonly JsonSerializerOptions SnapshotJsonOptions = new(JsonSerializerDefaults.Web);

    public static MenuItemOptionSnapshot Resolve(
        IReadOnlyCollection<MenuItemOptionGroup>? groups,
        IReadOnlyCollection<MenuItemOptionSelectionRequest>? selections)
    {
        var requested = new List<Guid>();
        foreach (var selection in selections ?? [])
        {
            if (selection.OptionId == Guid.Empty || requested.Contains(selection.OptionId))
            {
                throw new DomainException("انتخاب‌های آپشن تکراری یا نامعتبر است.");
            }

            requested.Add(selection.OptionId);
        }

        var components = new List<MenuItemOptionSnapshotComponent>();
        var ingredientUsage = new Dictionary<Guid, MenuItemOptionSnapshotIngredient>();
        var labels = new List<string>();
        var optionsTotal = 0m;

        var orderedGroups = (groups ?? [])
            .Where(group => group.IsActive)
            .OrderBy(group => group.DisplayOrder)
            .ThenBy(group => group.PersianName, StringComparer.Ordinal)
            .ToList();

        foreach (var group in orderedGroups)
        {
            var activeOptions = group.Options
                .Where(option => option.IsActive)
                .OrderBy(option => option.DisplayOrder)
                .ThenBy(option => option.PersianName, StringComparer.Ordinal)
                .ToList();

            var selectedInGroup = activeOptions
                .Where(option => requested.Contains(option.Id))
                .ToList();

            var requestedCount = selectedInGroup.Count;

            if (requestedCount < group.MinSelections)
            {
                throw new DomainException(group.MinSelections <= 1
                    ? $"انتخاب حداقل یک گزینه از «{group.PersianName}» الزامی است."
                    : $"حداقل {Number(group.MinSelections)} گزینه از «{group.PersianName}» را انتخاب کنید.");
            }

            if (requestedCount > group.MaxSelections)
            {
                throw new DomainException(
                    $"حداکثر {Number(group.MaxSelections)} گزینه از «{group.PersianName}» قابل انتخاب است.");
            }

            foreach (var option in selectedInGroup)
            {
                components.Add(new MenuItemOptionSnapshotComponent(
                    group.Id,
                    group.PersianName,
                    option.Id,
                    option.PersianName,
                    option.PriceDelta));

                optionsTotal += option.PriceDelta;
                labels.Add(option.PersianName);

                foreach (var ingredientRow in option.Ingredients)
                {
                    var perUnit = Math.Round(ingredientRow.Quantity, 3, MidpointRounding.AwayFromZero);
                    if (ingredientUsage.TryGetValue(ingredientRow.IngredientId, out var existing))
                    {
                        ingredientUsage[ingredientRow.IngredientId] = existing with
                        {
                            Quantity = existing.Quantity + perUnit
                        };
                    }
                    else
                    {
                        ingredientUsage[ingredientRow.IngredientId] = new MenuItemOptionSnapshotIngredient(
                            ingredientRow.IngredientId,
                            ingredientRow.Ingredient?.PersianName ?? string.Empty,
                            ingredientRow.Ingredient?.Unit ?? string.Empty,
                            perUnit);
                    }
                }
            }
        }

        // Option ids the customer sent that do not belong to this item's active
        // groups are rejected outright — silently ignoring them would mean the
        // customer pays (or is charged inventory) for choices nobody confirmed.
        var activeOptionIds = orderedGroups
            .SelectMany(group => group.Options)
            .Where(option => option.IsActive)
            .Select(option => option.Id)
            .ToHashSet();

        if (requested.Any(optionId => !activeOptionIds.Contains(optionId)))
        {
            throw new DomainException("یکی از گزینه‌های انتخاب‌شده بخشی از این محصول نیست یا موجود نیست.");
        }

        var roundedTotal = Math.Round(optionsTotal, 2, MidpointRounding.AwayFromZero);

        return new MenuItemOptionSnapshot(
            components,
            ingredientUsage.Values.ToList(),
            roundedTotal,
            labels);
    }

    /// <summary>
    /// True when every active group of the item is satisfyable from the given
    /// options (used by the public menu to render per-option availability).
    /// </summary>
    public static string Serialize(MenuItemOptionSnapshot snapshot) =>
        JsonSerializer.Serialize(snapshot, SnapshotJsonOptions);

    public static MenuItemOptionSnapshot? Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<MenuItemOptionSnapshot>(json, SnapshotJsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string Number(int value) => value.ToString("0", CultureInfo.InvariantCulture);
}
