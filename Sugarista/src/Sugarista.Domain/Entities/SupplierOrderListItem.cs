using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

public enum SupplierOrderListItemStatus
{
    Pending = 0,
    Ordered = 1,
    Received = 2,
    Cancelled = 3,
}

/// <summary>
/// Represents an item queued for restocking / ordering from a supplier
/// (e.g. out-of-stock cakes, pastries, desserts, or ad-hoc supplies),
/// ready to be exported to WhatsApp or PDF and tracked through delivery.
/// </summary>
public class SupplierOrderListItem : BaseAuditableEntity
{
    private SupplierOrderListItem()
    {
    }

    public SupplierOrderListItem(
        string itemName,
        decimal quantity,
        string unit = "عدد",
        Guid? menuItemId = null,
        string? categoryName = null,
        Guid? supplierId = null,
        string? supplierNameOverride = null,
        string? supplierPhoneOverride = null,
        string? notes = null,
        int? currentStockSnapshot = null)
    {
        MenuItemId = menuItemId;
        CategoryName = NormalizeOptional(categoryName, 120);
        CurrentStockSnapshot = currentStockSnapshot;
        Status = SupplierOrderListItemStatus.Pending;

        UpdateDetails(
            itemName,
            quantity,
            unit,
            supplierId,
            supplierNameOverride,
            supplierPhoneOverride,
            notes);
    }

    public Guid? MenuItemId { get; private set; }

    public MenuItem? MenuItem { get; private set; }

    public string ItemName { get; private set; } = string.Empty;

    public string? CategoryName { get; private set; }

    public Guid? SupplierId { get; private set; }

    public Supplier? Supplier { get; private set; }

    public string? SupplierNameOverride { get; private set; }

    public string? SupplierPhoneOverride { get; private set; }

    public decimal Quantity { get; private set; } = 1;

    public string Unit { get; private set; } = "عدد";

    public string? Notes { get; private set; }

    public SupplierOrderListItemStatus Status { get; private set; } = SupplierOrderListItemStatus.Pending;

    public string? BatchCode { get; private set; }

    public DateTime? OrderedAt { get; private set; }

    public DateTime? ReceivedAt { get; private set; }

    public int? CurrentStockSnapshot { get; private set; }

    public void UpdateDetails(
        string itemName,
        decimal quantity,
        string unit,
        Guid? supplierId,
        string? supplierNameOverride,
        string? supplierPhoneOverride,
        string? notes)
    {
        if (string.IsNullOrWhiteSpace(itemName))
        {
            throw new DomainException("نام محصول برای ثبت در لیست سفارش الزامی است.");
        }

        if (itemName.Trim().Length > 200)
        {
            throw new DomainException("نام محصول نباید بیشتر از ۲۰۰ کاراکتر باشد.");
        }

        if (quantity <= 0)
        {
            throw new DomainException("تعداد درخواستی باید بزرگ‌تر از صفر باشد.");
        }

        ItemName = itemName.Trim();
        Quantity = quantity;
        Unit = string.IsNullOrWhiteSpace(unit) ? "عدد" : NormalizeRequired(unit, 40);
        SupplierId = supplierId;
        SupplierNameOverride = NormalizeOptional(supplierNameOverride, 200);
        SupplierPhoneOverride = NormalizeOptional(supplierPhoneOverride, 40);
        Notes = NormalizeOptional(notes, 500);
    }

    public void AddQuantity(decimal delta, int? latestStockSnapshot = null)
    {
        if (delta <= 0)
        {
            throw new DomainException("مقدار افزوده باید بزرگ‌تر از صفر باشد.");
        }

        Quantity += delta;
        if (latestStockSnapshot.HasValue)
        {
            CurrentStockSnapshot = latestStockSnapshot.Value;
        }
    }

    public void MarkOrdered(string? batchCode, DateTime utcNow)
    {
        Status = SupplierOrderListItemStatus.Ordered;
        BatchCode = NormalizeOptional(batchCode, 40);
        OrderedAt = utcNow;
    }

    public void MarkReceived(DateTime utcNow)
    {
        Status = SupplierOrderListItemStatus.Received;
        ReceivedAt = utcNow;
        OrderedAt ??= utcNow;
    }

    public void MarkPending()
    {
        Status = SupplierOrderListItemStatus.Pending;
        OrderedAt = null;
        ReceivedAt = null;
        BatchCode = null;
    }

    public void Cancel()
    {
        Status = SupplierOrderListItemStatus.Cancelled;
    }

    private static string NormalizeRequired(string value, int maxLen)
    {
        var trimmed = value.Trim();
        return trimmed.Length > maxLen ? trimmed[..maxLen] : trimmed;
    }

    private static string? NormalizeOptional(string? value, int maxLen)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length > maxLen ? trimmed[..maxLen] : trimmed;
    }
}
