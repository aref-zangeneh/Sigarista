namespace Sugarista.Application.MenuItems.OptionGroups;

public record AdminMenuItemOptionGroupDto(
    Guid? Id,
    string PersianName,
    string? EnglishName,
    bool IsRequired,
    string SelectionMode,
    int MinSelections,
    int MaxSelections,
    int DisplayOrder,
    bool IsActive,
    IReadOnlyCollection<AdminMenuItemOptionDto> Options);

public record AdminMenuItemOptionDto(
    Guid? Id,
    string PersianName,
    string? EnglishName,
    decimal PriceDelta,
    bool IsDefault,
    bool IsActive,
    int DisplayOrder,
    IReadOnlyCollection<AdminMenuItemOptionIngredientDto> Ingredients);

public record AdminMenuItemOptionIngredientDto(Guid IngredientId, decimal Quantity);

public record MenuItemOptionGroupsDto(
    Guid MenuItemId,
    string MenuItemPersianName,
    IReadOnlyCollection<AdminMenuItemOptionGroupDto> Groups);

public record MenuItemOptionGroupInput(
    string PersianName,
    string? EnglishName,
    bool IsRequired,
    string SelectionMode,
    int MinSelections,
    int MaxSelections,
    int DisplayOrder,
    bool IsActive,
    IReadOnlyCollection<MenuItemOptionInput> Options);

public record MenuItemOptionInput(
    string PersianName,
    string? EnglishName,
    decimal PriceDelta,
    bool IsDefault,
    bool IsActive,
    int DisplayOrder,
    IReadOnlyCollection<MenuItemOptionIngredientInput> Ingredients);

public record MenuItemOptionIngredientInput(Guid IngredientId, decimal Quantity);
