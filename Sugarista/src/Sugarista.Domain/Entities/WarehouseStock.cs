using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

/// <summary>
/// Stock of one ingredient inside one admin-defined warehouse (انبار/انبارک).
/// Rows live in a dedicated table so the legacy per-ingredient
/// <see cref="InventoryStock"/> (the "main" stock) is never touched.
/// </summary>
public class WarehouseStock : BaseAuditableEntity
{
    public WarehouseStock(
        Guid warehouseId,
        Guid ingredientId,
        decimal minAlertQuantity = 0)
    {
        if (warehouseId == Guid.Empty)
        {
            throw new DomainException("Warehouse is required.");
        }

        if (ingredientId == Guid.Empty)
        {
            throw new DomainException("Ingredient is required.");
        }

        WarehouseId = warehouseId;
        IngredientId = ingredientId;
        Quantity = 0;
        MinAlertQuantity = minAlertQuantity >= 0 ? minAlertQuantity : 0;
        LastUpdatedAt = DateTimeOffset.UtcNow;
    }

    private WarehouseStock()
    {
    }

    public Guid WarehouseId { get; private set; }

    public Warehouse Warehouse { get; private set; } = null!;

    public Guid IngredientId { get; private set; }

    public Ingredient Ingredient { get; private set; } = null!;

    public decimal Quantity { get; private set; }

    public decimal MinAlertQuantity { get; private set; }

    public DateTimeOffset LastUpdatedAt { get; private set; }

    public bool IsLowStock => Quantity < MinAlertQuantity;

    public StockMovement Restock(decimal quantity, string? note)
    {
        if (quantity <= 0)
        {
            throw new DomainException("Restock quantity must be greater than zero.");
        }

        Quantity += quantity;
        LastUpdatedAt = DateTimeOffset.UtcNow;

        return StockMovement.Create(IngredientId, quantity, StockMovementType.Restock, note, sourceMenuItemName: null, warehouseId: WarehouseId);
    }

    public StockMovement Adjust(decimal delta, string? note)
    {
        if (delta == 0)
        {
            throw new DomainException("Adjustment delta must not be zero.");
        }

        Quantity += delta;
        LastUpdatedAt = DateTimeOffset.UtcNow;

        return StockMovement.Create(IngredientId, delta, StockMovementType.ManualAdjustment, note, sourceMenuItemName: null, warehouseId: WarehouseId);
    }

    public void SetAlertThreshold(decimal minAlertQuantity)
    {
        if (minAlertQuantity < 0)
        {
            throw new DomainException("Minimum alert quantity must be greater than or equal to zero.");
        }

        MinAlertQuantity = minAlertQuantity;
        LastUpdatedAt = DateTimeOffset.UtcNow;
    }

    public StockMovement TransferOut(decimal quantity, string? note)
    {
        if (quantity <= 0)
        {
            throw new DomainException("مقدار انتقال باید بزرگ‌تر از صفر باشد.");
        }

        if (Quantity < quantity)
        {
            throw new DomainException(
                $"موجودی کافی در انبار مبدا نیست. موجود: {Quantity:0.###} — درخواست انتقال: {quantity:0.###}.");
        }

        Quantity -= quantity;
        LastUpdatedAt = DateTimeOffset.UtcNow;

        return StockMovement.Create(
            IngredientId,
            -quantity,
            StockMovementType.TransferOut,
            note,
            sourceMenuItemName: null,
            warehouseId: WarehouseId);
    }

    public StockMovement TransferIn(decimal quantity, string? note)
    {
        if (quantity <= 0)
        {
            throw new DomainException("مقدار انتقال باید بزرگ‌تر از صفر باشد.");
        }

        Quantity += quantity;
        LastUpdatedAt = DateTimeOffset.UtcNow;

        return StockMovement.Create(
            IngredientId,
            quantity,
            StockMovementType.TransferIn,
            note,
            sourceMenuItemName: null,
            warehouseId: WarehouseId);
    }
}
