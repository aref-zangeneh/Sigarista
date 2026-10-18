using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

public class Ingredient : BaseAuditableEntity
{
    public static readonly IReadOnlyList<string> ValidUnits = ["ml", "l", "gr", "kg", "oz", "tsp", "tbsp", "cup", "pieces"];

    public static readonly IReadOnlyList<string> ValidLocations = ["main", "bar", "kitchen", "dry", "refrigerator"];

    public static readonly IReadOnlyList<string> ValidKinds = ["ingredient", "packaging"];

    public Ingredient()
    {
    }

    public Ingredient(
        string persianName,
        string englishName,
        string unit,
        string description,
        int displayOrder,
        bool isActive = true,
        string inventoryLocation = "main",
        decimal unitCost = 0,
        string kind = "ingredient")
    {
        Update(persianName, englishName, unit, description, displayOrder, isActive, inventoryLocation, unitCost, kind);
    }

    public string Name { get; private set; } = string.Empty;

    public string PersianName { get; private set; } = string.Empty;

    public string EnglishName { get; private set; } = string.Empty;

    public string Unit { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public int DisplayOrder { get; private set; }

    public bool IsActive { get; private set; } = true;

    public string InventoryLocation { get; private set; } = "main";

    /// <summary>
    /// Discriminates raw consumable materials from packaging items (cups, lids, ...).
    /// Both share the same stock/purchase pipeline; recipes may only use "ingredient".
    /// </summary>
    public string Kind { get; private set; } = "ingredient";

    public decimal UnitCost { get; private set; }

    public ICollection<RecipeItem> RecipeItems { get; private set; } = [];

    public ICollection<IngredientSupplier> IngredientSuppliers { get; private set; } = [];

    public InventoryStock? Stock { get; private set; }

    public void Update(
        string persianName,
        string englishName,
        string unit,
        string description,
        int displayOrder,
        bool isActive,
        string inventoryLocation = "main",
        decimal unitCost = 0,
        string kind = "ingredient")
    {
        if (string.IsNullOrWhiteSpace(persianName))
        {
            throw new DomainException("Ingredient Persian name is required.");
        }

        if (string.IsNullOrWhiteSpace(englishName))
        {
            throw new DomainException("Ingredient English name is required.");
        }

        if (string.IsNullOrWhiteSpace(unit) || !ValidUnits.Contains(unit, StringComparer.OrdinalIgnoreCase))
        {
            throw new DomainException($"Ingredient unit must be one of: {string.Join(", ", ValidUnits)}.");
        }

        if (string.IsNullOrWhiteSpace(inventoryLocation) || !ValidLocations.Contains(inventoryLocation, StringComparer.OrdinalIgnoreCase))
        {
            throw new DomainException($"Inventory location must be one of: {string.Join(", ", ValidLocations)}.");
        }

        if (string.IsNullOrWhiteSpace(kind) || !ValidKinds.Contains(kind, StringComparer.OrdinalIgnoreCase))
        {
            throw new DomainException($"Kind must be one of: {string.Join(", ", ValidKinds)}.");
        }

        PersianName = persianName.Trim();
        EnglishName = englishName.Trim();
        Name = PersianName;
        Unit = unit.Trim().ToLowerInvariant();
        Description = description.Trim();
        DisplayOrder = displayOrder;
        IsActive = isActive;
        InventoryLocation = inventoryLocation.Trim().ToLowerInvariant();
        Kind = kind.Trim().ToLowerInvariant();
        UnitCost = unitCost < 0 ? 0 : unitCost;
    }
}
