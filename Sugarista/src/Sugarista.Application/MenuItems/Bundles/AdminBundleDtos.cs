namespace Sugarista.Application.MenuItems.Bundles;

public sealed record AdminBundleComponentDto(
    Guid Id,
    Guid ComponentMenuItemId,
    string PersianName,
    string EnglishName,
    string? ImageUrl,
    string? SizeKey,
    string? SizeLabel,
    int Quantity,
    decimal UnitRegularPrice,
    decimal LineRegularPrice,
    decimal UnitCost,
    decimal LineCost,
    bool IsAvailable,
    int DisplayOrder);

public sealed record AdminMenuItemBundleDto(
    Guid MenuItemId,
    string PersianName,
    string EnglishName,
    bool IsBundle,
    decimal BundlePrice,
    decimal RegularTotal,
    decimal SavingsAmount,
    int SavingsPercent,
    decimal TotalCostPerServing,
    decimal EstimatedProfit,
    decimal? EstimatedMarginPercent,
    bool AllComponentsAvailable,
    IReadOnlyCollection<AdminBundleComponentDto> Components);

public sealed record UpsertBundleComponentInput(
    Guid ComponentMenuItemId,
    int Quantity,
    string? SizeKey,
    int DisplayOrder);
