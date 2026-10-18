using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

public class InventoryPurchase : BaseAuditableEntity
{
    private InventoryPurchase()
    {
    }

    public InventoryPurchase(
        Guid ingredientId,
        decimal quantity,
        int? packageCount,
        decimal? packageSize,
        DateOnly expirationDate,
        decimal remainingQuantity,
        decimal? purchaseUnitPrice,
        decimal shippingFee,
        decimal taxAmount,
        decimal incidentalCost,
        string? description,
        string? documentImageUrl,
        Guid? supplierId = null)
    {
        if (ingredientId == Guid.Empty)
        {
            throw new DomainException("Ingredient is required.");
        }

        if (quantity <= 0)
        {
            throw new DomainException("Purchase quantity must be greater than zero.");
        }

        if (remainingQuantity < 0 || remainingQuantity > quantity)
        {
            throw new DomainException("Remaining quantity must be between zero and purchase quantity.");
        }

        if (packageCount is <= 0)
        {
            throw new DomainException("Package count must be greater than zero when provided.");
        }

        if (packageSize is <= 0)
        {
            throw new DomainException("Package size must be greater than zero when provided.");
        }

        IngredientId = ingredientId;
        SupplierId = supplierId == Guid.Empty ? null : supplierId;
        Quantity = quantity;
        PackageCount = packageCount;
        PackageSize = packageSize;
        ExpirationDate = expirationDate;
        RemainingQuantity = remainingQuantity;
        PurchaseUnitPrice = purchaseUnitPrice is < 0 ? null : purchaseUnitPrice;
        ShippingFee = shippingFee < 0 ? 0 : shippingFee;
        TaxAmount = taxAmount < 0 ? 0 : taxAmount;
        IncidentalCost = incidentalCost < 0 ? 0 : incidentalCost;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        DocumentImageUrl = string.IsNullOrWhiteSpace(documentImageUrl) ? null : documentImageUrl.Trim();
        ReceivedAt = DateTimeOffset.UtcNow;
    }

    public Guid IngredientId { get; private set; }

    public Ingredient Ingredient { get; private set; } = null!;

    public Guid? SupplierId { get; private set; }

    public Supplier? Supplier { get; private set; }

    public decimal Quantity { get; private set; }

    public int? PackageCount { get; private set; }

    public decimal? PackageSize { get; private set; }

    public DateOnly ExpirationDate { get; private set; }

    public decimal RemainingQuantity { get; private set; }

    public decimal? PurchaseUnitPrice { get; private set; }

    public decimal ShippingFee { get; private set; }

    public decimal TaxAmount { get; private set; }

    public decimal IncidentalCost { get; private set; }

    public string? Description { get; private set; }

    public string? DocumentImageUrl { get; private set; }

    public DateTimeOffset ReceivedAt { get; private set; }

    public Guid? StockMovementId { get; private set; }

    public StockMovement? StockMovement { get; private set; }

    public decimal TotalCost =>
        (PurchaseUnitPrice ?? 0) * Quantity + ShippingFee + TaxAmount + IncidentalCost;

    public bool IsExpired(DateOnly today) => ExpirationDate < today;

    public bool IsExpiringSoon(DateOnly today, int withinDays = 7) =>
        !IsExpired(today) && ExpirationDate <= today.AddDays(withinDays);

    public void LinkStockMovement(Guid stockMovementId)
    {
        if (stockMovementId == Guid.Empty)
        {
            throw new DomainException("Stock movement is required.");
        }

        StockMovementId = stockMovementId;
    }

    public void Deduct(decimal quantity)
    {
        if (quantity <= 0)
        {
            throw new DomainException("Deduction quantity must be greater than zero.");
        }

        RemainingQuantity = Math.Max(0, RemainingQuantity - quantity);
    }

    public void Restore(decimal quantity)
    {
        if (quantity <= 0)
        {
            throw new DomainException("Restore quantity must be greater than zero.");
        }

        RemainingQuantity = Math.Min(Quantity, RemainingQuantity + quantity);
    }
}
