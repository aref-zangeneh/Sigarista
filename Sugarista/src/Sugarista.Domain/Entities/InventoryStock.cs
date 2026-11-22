using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

public class InventoryStock : BaseAuditableEntity
{
    public InventoryStock()
    {
    }

    public InventoryStock(Guid ingredientId, decimal minAlertQuantity = 0)
    {
        if (ingredientId == Guid.Empty)
        {
            throw new DomainException("InventoryStock ingredient is required.");
        }

        IngredientId = ingredientId;
        CurrentQuantity = 0;
        MinAlertQuantity = minAlertQuantity >= 0 ? minAlertQuantity : 0;
        LastUpdatedAt = DateTimeOffset.UtcNow;
    }

    public Guid IngredientId { get; private set; }

    public Ingredient Ingredient { get; private set; } = null!;

    public decimal CurrentQuantity { get; private set; }

    public decimal MinAlertQuantity { get; private set; }

    public DateTimeOffset LastUpdatedAt { get; private set; }

    public bool IsLowStock => CurrentQuantity < MinAlertQuantity;

    public ICollection<StockMovement> Movements { get; private set; } = [];

    public void SetAlertThreshold(decimal minAlertQuantity)
    {
        if (minAlertQuantity < 0)
        {
            throw new DomainException("Minimum alert quantity must be greater than or equal to zero.");
        }

        MinAlertQuantity = minAlertQuantity;
        LastUpdatedAt = DateTimeOffset.UtcNow;
    }

    public StockMovement Restock(decimal quantity, string note)
    {
        if (quantity <= 0)
        {
            throw new DomainException("Restock quantity must be greater than zero.");
        }

        CurrentQuantity += quantity;
        LastUpdatedAt = DateTimeOffset.UtcNow;

        return StockMovement.Create(IngredientId, quantity, StockMovementType.Restock, note, sourceMenuItemName: null);
    }

    public StockMovement Adjust(decimal delta, string note)
    {
        if (delta == 0)
        {
            throw new DomainException("Adjustment delta must not be zero.");
        }

        CurrentQuantity += delta;
        LastUpdatedAt = DateTimeOffset.UtcNow;

        return StockMovement.Create(IngredientId, delta, StockMovementType.ManualAdjustment, note, sourceMenuItemName: null);
    }

    public StockMovement Deduct(decimal quantity, string menuItemName, Guid? orderId = null, Guid? orderLineId = null)
    {
        if (quantity <= 0)
        {
            throw new DomainException("Deduction quantity must be greater than zero.");
        }

        CurrentQuantity -= quantity;
        LastUpdatedAt = DateTimeOffset.UtcNow;

        return StockMovement.Create(
            IngredientId,
            -quantity,
            StockMovementType.SaleDeduction,
            note: null,
            sourceMenuItemName: menuItemName,
            orderId,
            orderLineId);
    }

    public StockMovement DeductForPackaging(decimal quantity, string deliveryMethodName, Guid? orderId = null)
    {
        if (quantity <= 0)
        {
            throw new DomainException("Deduction quantity must be greater than zero.");
        }

        CurrentQuantity -= quantity;
        LastUpdatedAt = DateTimeOffset.UtcNow;

        return StockMovement.Create(
            IngredientId,
            -quantity,
            StockMovementType.PackagingConsumption,
            note: $"بسته‌بندی روش تحویل {deliveryMethodName}",
            sourceMenuItemName: deliveryMethodName,
            orderId: orderId);
    }

    public StockMovement DeductForProduction(decimal quantity, string menuItemName, Guid productionBatchId)
    {
        if (quantity <= 0)
        {
            throw new DomainException("Deduction quantity must be greater than zero.");
        }

        CurrentQuantity -= quantity;
        LastUpdatedAt = DateTimeOffset.UtcNow;

        return StockMovement.Create(
            IngredientId,
            -quantity,
            StockMovementType.ProductionDeduction,
            note: "تولید محصول",
            sourceMenuItemName: menuItemName,
            productionBatchId: productionBatchId);
    }

    public StockMovement Restore(decimal quantity, string menuItemName, string? note = null, Guid? orderId = null, Guid? orderLineId = null)
    {
        if (quantity <= 0)
        {
            throw new DomainException("Restore quantity must be greater than zero.");
        }

        CurrentQuantity += quantity;
        LastUpdatedAt = DateTimeOffset.UtcNow;

        return StockMovement.Create(
            IngredientId,
            quantity,
            StockMovementType.SaleReversal,
            note,
            menuItemName,
            orderId,
            orderLineId);
    }

    public StockMovement TransferOut(decimal quantity, string? note)
    {
        if (quantity <= 0)
        {
            throw new DomainException("مقدار انتقال باید بزرگ‌تر از صفر باشد.");
        }

        if (CurrentQuantity < quantity)
        {
            throw new DomainException(
                $"موجودی کافی در انبار اصلی نیست. موجود: {CurrentQuantity:0.###} — درخواست انتقال: {quantity:0.###}.");
        }

        CurrentQuantity -= quantity;
        LastUpdatedAt = DateTimeOffset.UtcNow;

        return StockMovement.Create(
            IngredientId,
            -quantity,
            StockMovementType.TransferOut,
            note,
            sourceMenuItemName: null,
            warehouseId: null);
    }

    public StockMovement TransferIn(decimal quantity, string? note)
    {
        if (quantity <= 0)
        {
            throw new DomainException("مقدار انتقال باید بزرگ‌تر از صفر باشد.");
        }

        CurrentQuantity += quantity;
        LastUpdatedAt = DateTimeOffset.UtcNow;

        return StockMovement.Create(
            IngredientId,
            quantity,
            StockMovementType.TransferIn,
            note,
            sourceMenuItemName: null,
            warehouseId: null);
    }

    public StockMovement ReceiveVoucher(decimal quantity, string? note)
    {
        if (quantity <= 0)
        {
            throw new DomainException("مقدار ورود به انبار باید بزرگ‌تر از صفر باشد.");
        }

        CurrentQuantity += quantity;
        LastUpdatedAt = DateTimeOffset.UtcNow;

        return StockMovement.Create(
            IngredientId,
            quantity,
            StockMovementType.WarehouseReceipt,
            note,
            sourceMenuItemName: null,
            warehouseId: null);
    }

    public StockMovement IssueVoucher(decimal quantity, string? note)
    {
        if (quantity <= 0)
        {
            throw new DomainException("مقدار خروج از انبار باید بزرگ‌تر از صفر باشد.");
        }

        if (CurrentQuantity < quantity)
        {
            throw new DomainException(
                $"موجودی کافی در انبار اصلی نیست. موجود: {CurrentQuantity:0.###} — درخواست خروج: {quantity:0.###}.");
        }

        CurrentQuantity -= quantity;
        LastUpdatedAt = DateTimeOffset.UtcNow;

        return StockMovement.Create(
            IngredientId,
            -quantity,
            StockMovementType.WarehouseIssue,
            note,
            sourceMenuItemName: null,
            warehouseId: null);
    }

    public StockMovement ReverseVoucher(decimal delta, string? note)
    {
        if (delta == 0)
        {
            throw new DomainException("مقدار برگشت سند نمی‌تواند صفر باشد.");
        }

        if (delta < 0 && CurrentQuantity < Math.Abs(delta))
        {
            throw new DomainException(
                $"موجودی فعلی انبار اصلی برای ابطال این رسید کافی نیست. موجود: {CurrentQuantity:0.###} — مقدار لازم: {Math.Abs(delta):0.###}.");
        }

        CurrentQuantity += delta;
        LastUpdatedAt = DateTimeOffset.UtcNow;

        return StockMovement.Create(
            IngredientId,
            delta,
            StockMovementType.VoucherReversal,
            note,
            sourceMenuItemName: null,
            warehouseId: null);
    }
}
