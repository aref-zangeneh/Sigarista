using System.Globalization;
using System.Text.Json;
using Sugarista.Domain.Entities;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Application.DrinkBuilder;

/// <summary>One option amount chosen by the customer.</summary>
public sealed record DrinkBuilderSelectionRequest(Guid OptionId, decimal Amount);

/// <summary>The custom drink attached to an order line.</summary>
public sealed record DrinkBuilderLineRequest(
    Guid ProfileId,
    IReadOnlyCollection<DrinkBuilderSelectionRequest>? Selections = null);

/// <summary>Immutable snapshot of one component stored with the order line.</summary>
public sealed record DrinkBuilderSnapshotComponent(
    Guid GroupId,
    string GroupName,
    string InputType,
    Guid OptionId,
    string OptionName,
    string Unit,
    decimal Amount,
    decimal PricePerUnit,
    decimal AmountPrice);

/// <summary>
/// Immutable snapshot of a validated custom drink. It is serialised into
/// <see cref="OrderLine.CustomizationJson"/> so the composition never changes
/// even when the admin later edits prices or options.
/// </summary>
public sealed record DrinkBuilderSnapshot(
    Guid ProfileId,
    string ProfileName,
    Guid MenuItemId,
    decimal BasePrice,
    decimal ComponentsTotal,
    decimal UnitPrice,
    IReadOnlyCollection<DrinkBuilderSnapshotComponent> Components,
    IReadOnlyCollection<string> SummaryLabels);

/// <summary>
/// Single source of truth for custom drink validation and pricing. The
/// backend always recomputes the price here; the client price is only used
/// for the live preview.
/// </summary>
public static class DrinkBuilderPricing
{
    private static readonly JsonSerializerOptions SnapshotJsonOptions = new(JsonSerializerDefaults.Web);

    public static DrinkBuilderSnapshot Resolve(
        DrinkBuilderProfile profile,
        decimal basePrice,
        IReadOnlyCollection<DrinkBuilderSelectionRequest>? selections)
    {
        if (profile is null)
        {
            throw new DomainException("پروفایل نوشیدنی سفارشی یافت نشد.");
        }

        if (basePrice < 0)
        {
            throw new DomainException("قیمت پایهٔ نوشیدنی معتبر نیست.");
        }

        var requested = new Dictionary<Guid, decimal>();
        foreach (var selection in selections ?? [])
        {
            if (selection.OptionId == Guid.Empty)
            {
                throw new DomainException("شناسهٔ گزینهٔ نوشیدنی معتبر نیست.");
            }

            if (selection.Amount < 0)
            {
                throw new DomainException("مقدار گزینهٔ نوشیدنی نمی‌تواند منفی باشد.");
            }

            if (!requested.TryAdd(selection.OptionId, selection.Amount))
            {
                throw new DomainException("یک گزینهٔ نوشیدنی بیش از یک بار ارسال شده است.");
            }
        }

        var knownOptionIds = profile.Groups
            .SelectMany(group => group.Options)
            .Select(option => option.Id)
            .ToHashSet();

        if (requested.Keys.Any(optionId => !knownOptionIds.Contains(optionId)))
        {
            throw new DomainException("یکی از گزینه‌های انتخاب‌شده بخشی از این نوشیدنی نیست.");
        }

        // A submitted composition is authoritative. Defaults are only applied to
        // groups the customer did not address at all; once a group contains any
        // explicit selection, every other option in that group is treated as
        // "not selected" instead of silently falling back to its default.
        var explicitGroupIds = profile.Groups
            .Where(group => group.Options.Any(option => requested.ContainsKey(option.Id)))
            .Select(group => group.Id)
            .ToHashSet();

        var components = new List<DrinkBuilderSnapshotComponent>();
        var componentsTotal = 0m;

        foreach (var group in profile.Groups
                     .OrderBy(group => group.DisplayOrder)
                     .ThenBy(group => group.Name, StringComparer.Ordinal))
        {
            var selectedCount = 0;
            var groupIsExplicit = explicitGroupIds.Contains(group.Id);

            foreach (var option in group.Options
                         .OrderBy(option => option.DisplayOrder)
                         .ThenBy(option => option.Name, StringComparer.Ordinal))
            {
                var hasRequestedAmount = requested.TryGetValue(option.Id, out var requestedAmount);

                if (!option.IsActive)
                {
                    if (hasRequestedAmount && requestedAmount > 0)
                    {
                        throw new DomainException($"گزینهٔ «{option.Name}» در حال حاضر موجود نیست.");
                    }

                    continue;
                }

                if (group.InputType == DrinkBuilderInputType.Choice)
                {
                    if (hasRequestedAmount && requestedAmount is not (0m or 1m))
                    {
                        throw new DomainException($"مقدار گزینهٔ «{option.Name}» باید ۰ یا ۱ باشد.");
                    }

                    var isSelected = hasRequestedAmount
                        ? requestedAmount == 1m
                        : !groupIsExplicit && option.IsDefault;

                    if (!isSelected)
                    {
                        continue;
                    }

                    var choicePrice = Math.Round(option.PricePerUnit, 2, MidpointRounding.AwayFromZero);
                    components.Add(new DrinkBuilderSnapshotComponent(
                        group.Id,
                        group.Name,
                        nameof(DrinkBuilderInputType.Choice),
                        option.Id,
                        option.Name,
                        option.Unit.ToString(),
                        1m,
                        option.PricePerUnit,
                        choicePrice));

                    componentsTotal += choicePrice;
                    selectedCount++;
                    continue;
                }

                var amount = hasRequestedAmount
                    ? requestedAmount
                    : groupIsExplicit ? 0m : option.DefaultAmount;

                if (amount == 0m)
                {
                    continue;
                }

                if (amount < option.MinAmount || amount > option.MaxAmount)
                {
                    throw new DomainException(
                        $"مقدار «{option.Name}» باید بین {Format(option.MinAmount)} و {Format(option.MaxAmount)} {UnitLabel(option.Unit)} باشد.");
                }

                if (!DrinkBuilderOption.IsAligned(amount, option.MinAmount, option.Step))
                {
                    throw new DomainException(
                        $"مقدار «{option.Name}» باید مضربی از {Format(option.Step)} {UnitLabel(option.Unit)} باشد.");
                }

                var amountPrice = Math.Round(amount * option.PricePerUnit, 2, MidpointRounding.AwayFromZero);
                components.Add(new DrinkBuilderSnapshotComponent(
                    group.Id,
                    group.Name,
                    nameof(DrinkBuilderInputType.Slider),
                    option.Id,
                    option.Name,
                    option.Unit.ToString(),
                    amount,
                    option.PricePerUnit,
                    amountPrice));

                componentsTotal += amountPrice;
                selectedCount++;
            }

            var minimum = Math.Max(group.IsRequired ? 1 : 0, group.MinSelections);

            if (selectedCount < minimum)
            {
                throw new DomainException(minimum <= 1
                    ? $"انتخاب حداقل یک گزینه از «{group.Name}» الزامی است."
                    : $"حداقل {Format(minimum)} گزینه از «{group.Name}» را انتخاب کنید.");
            }

            if (selectedCount > group.MaxSelections)
            {
                throw new DomainException(
                    $"حداکثر {Format(group.MaxSelections)} گزینه از «{group.Name}» قابل انتخاب است.");
            }
        }

        var roundedComponentsTotal = Math.Round(componentsTotal, 2, MidpointRounding.AwayFromZero);
        var unitPrice = Math.Round(basePrice + roundedComponentsTotal, 2, MidpointRounding.AwayFromZero);

        var summaryLabels = components
            .Select(component => component.InputType == nameof(DrinkBuilderInputType.Choice)
                ? component.OptionName
                : $"{component.OptionName} {Format(component.Amount)} {UnitLabel(ParseUnit(component.Unit))}")
            .ToList();

        return new DrinkBuilderSnapshot(
            profile.Id,
            profile.Name,
            profile.MenuItemId,
            basePrice,
            roundedComponentsTotal,
            unitPrice,
            components,
            summaryLabels);
    }

    /// <summary>
    /// Recomputes the unit price of an existing order line when an admin edits
    /// it. The base price follows the current menu price, while the custom
    /// component total is taken from the stored snapshot so editing a quantity
    /// never silently drops the customer's paid customisation.
    /// </summary>
    public static decimal ResolveEditedUnitPrice(decimal baseUnitPrice, string? customizationJson)
    {
        var snapshot = Deserialize(customizationJson);

        return snapshot is null
            ? baseUnitPrice
            : Math.Round(baseUnitPrice + snapshot.ComponentsTotal, 2, MidpointRounding.AwayFromZero);
    }

    public static string Serialize(DrinkBuilderSnapshot snapshot) =>
        JsonSerializer.Serialize(snapshot, SnapshotJsonOptions);

    public static DrinkBuilderSnapshot? Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<DrinkBuilderSnapshot>(json, SnapshotJsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static string UnitLabel(DrinkBuilderUnit unit) => unit switch
    {
        DrinkBuilderUnit.Gram => "گرم",
        DrinkBuilderUnit.Milliliter => "میلی‌لیتر",
        DrinkBuilderUnit.Piece => "عدد",
        DrinkBuilderUnit.Shot => "شات",
        DrinkBuilderUnit.Pump => "پمپ",
        _ => string.Empty,
    };

    private static DrinkBuilderUnit ParseUnit(string unit) =>
        Enum.TryParse<DrinkBuilderUnit>(unit, true, out var parsed) ? parsed : DrinkBuilderUnit.Gram;

    private static string Format(decimal value) =>
        value.ToString("0.###", CultureInfo.InvariantCulture);
}
