using Sugarista.Domain.Common;

namespace Sugarista.Domain.Entities;

public enum StockMovementType
{
    Restock,
    ManualAdjustment,
    SaleDeduction,
}

public class StockMovement : BaseEntity
{
    private StockMovement()
    {
    }

    public Guid IngredientId { get; private set; }

    public InventoryStock Stock { get; private set; } = null!;

    /// <summary>Positive for restocks/adjustments up, negative for deductions.</summary>
    public decimal Quantity { get; private set; }

    public StockMovementType MovementType { get; private set; }

    public string? Note { get; private set; }

    public string? SourceMenuItemName { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    internal static StockMovement Create(
        Guid ingredientId,
        decimal quantity,
        StockMovementType movementType,
        string? note,
        string? sourceMenuItemName)
    {
        return new StockMovement
        {
            IngredientId = ingredientId,
            Quantity = quantity,
            MovementType = movementType,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            SourceMenuItemName = string.IsNullOrWhiteSpace(sourceMenuItemName) ? null : sourceMenuItemName.Trim(),
            OccurredAt = DateTimeOffset.UtcNow,
        };
    }
}
