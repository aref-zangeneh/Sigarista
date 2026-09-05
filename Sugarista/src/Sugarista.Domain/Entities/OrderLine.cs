using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

public class OrderLine : BaseEntity
{
    private OrderLine()
    {
    }

    public OrderLine(
        Guid orderId,
        Guid menuItemId,
        string persianName,
        string englishName,
        string? sizeKey,
        string? sizeLabel,
        int quantity,
        decimal unitPrice,
        IReadOnlyCollection<string> modifierLabels,
        string? payerKey = null,
        string? payerLabel = null,
        string? baristaNote = null)
    {
        if (orderId == Guid.Empty)
        {
            throw new DomainException("Order line order is required.");
        }

        if (menuItemId == Guid.Empty)
        {
            throw new DomainException("Order line menu item is required.");
        }

        if (string.IsNullOrWhiteSpace(persianName))
        {
            throw new DomainException("Order line Persian name is required.");
        }

        if (quantity <= 0)
        {
            throw new DomainException("Order line quantity must be greater than zero.");
        }

        if (unitPrice < 0)
        {
            throw new DomainException("Order line unit price must be greater than or equal to zero.");
        }

        OrderId = orderId;
        MenuItemId = menuItemId;
        PersianName = persianName.Trim();
        EnglishName = englishName.Trim();
        SizeKey = string.IsNullOrWhiteSpace(sizeKey) ? null : sizeKey.Trim().ToLowerInvariant();
        SizeLabel = string.IsNullOrWhiteSpace(sizeLabel) ? null : sizeLabel.Trim();
        Quantity = quantity;
        UnitPrice = unitPrice;
        LineTotal = unitPrice * quantity;
        ModifierLabels = NormalizeList(modifierLabels);
        AssignPayer(payerKey, payerLabel);
        BaristaNote = string.IsNullOrWhiteSpace(baristaNote) ? null : baristaNote.Trim();
        InventoryDeductedQuantity = 0;
    }

    public Guid OrderId { get; private set; }

    public Order Order { get; private set; } = null!;

    public Guid MenuItemId { get; private set; }

    public MenuItem MenuItem { get; private set; } = null!;

    public string PersianName { get; private set; } = string.Empty;

    public string EnglishName { get; private set; } = string.Empty;

    public string? SizeKey { get; private set; }

    public string? SizeLabel { get; private set; }

    public int Quantity { get; private set; }

    public decimal UnitPrice { get; private set; }

    public decimal LineTotal { get; private set; }

    public string[] ModifierLabels { get; private set; } = [];

    public string? PayerKey { get; private set; }

    public string? PayerLabel { get; private set; }

    public int InventoryDeductedQuantity { get; private set; }

    public string? BaristaNote { get; private set; }

    public void UpdateBaristaNote(string? baristaNote)
    {
        BaristaNote = string.IsNullOrWhiteSpace(baristaNote) ? null : baristaNote.Trim();
    }

    public void AssignPayer(string? payerKey, string? payerLabel)
    {
        PayerKey = string.IsNullOrWhiteSpace(payerKey) ? null : payerKey.Trim().ToLowerInvariant();
        PayerLabel = string.IsNullOrWhiteSpace(payerLabel)
            ? PayerKey
            : payerLabel.Trim();
    }

    public void UpdateQuantity(int quantity)
    {
        if (quantity <= 0)
        {
            throw new DomainException("Order line quantity must be greater than zero.");
        }

        Quantity = quantity;
        LineTotal = UnitPrice * quantity;
    }

    public void UpdateUnitPrice(decimal unitPrice)
    {
        if (unitPrice < 0)
        {
            throw new DomainException("Order line unit price must be greater than or equal to zero.");
        }

        UnitPrice = unitPrice;
        LineTotal = unitPrice * Quantity;
    }

    public void MarkInventoryDeducted(int quantity)
    {
        if (quantity < 0 || quantity > Quantity)
        {
            throw new DomainException("Invalid inventory deducted quantity.");
        }

        InventoryDeductedQuantity = quantity;
    }

    public void ResetInventoryDeduction()
    {
        InventoryDeductedQuantity = 0;
    }

    private static string[] NormalizeList(IReadOnlyCollection<string> values)
    {
        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
