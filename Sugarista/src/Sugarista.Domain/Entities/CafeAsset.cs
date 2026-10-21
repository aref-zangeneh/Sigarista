using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

/// <summary>
/// One registered item of café property (اموال و تجهیزات): a single kind of
/// asset with its count, unit purchase price and photo gallery. Assets stay
/// in the ledger forever — «خارج از رده» is a state, not a deletion.
/// </summary>
public class CafeAsset : BaseAuditableEntity
{
    private CafeAsset()
    {
    }

    public CafeAsset(
        string persianName,
        string? englishName,
        string category,
        string? serialNumber,
        string location,
        DateOnly? purchaseDate,
        decimal unitPrice,
        int quantity,
        string? supplierName,
        string? note)
    {
        SetName(persianName);
        SetCategory(category);
        SetLocation(location);
        SetUnitPrice(unitPrice);
        SetQuantity(quantity);

        EnglishName = string.IsNullOrWhiteSpace(englishName) ? null : englishName.Trim();
        SerialNumber = string.IsNullOrWhiteSpace(serialNumber) ? null : serialNumber.Trim();
        SupplierName = string.IsNullOrWhiteSpace(supplierName) ? null : supplierName.Trim();
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        PurchaseDate = purchaseDate;
        IsActive = true;
        Photos = [];
    }

    public string PersianName { get; private set; } = string.Empty;

    public string? EnglishName { get; private set; }

    public string Category { get; private set; } = string.Empty;

    public string? SerialNumber { get; private set; }

    /// <summary>Where the item physically lives (سالن، آشپزخانه، انبار، ...).</summary>
    public string Location { get; private set; } = string.Empty;

    public DateOnly? PurchaseDate { get; private set; }

    /// <summary>Unit purchase price (تومان).</summary>
    public decimal UnitPrice { get; private set; }

    public int Quantity { get; private set; }

    public string? SupplierName { get; private set; }

    public string? Note { get; private set; }

    /// <summary>false = خارج از رده (فروخته‌شده، از کار افتاده، ...).</summary>
    public bool IsActive { get; private set; }

    public string? DisposalNote { get; private set; }

    public DateTimeOffset? DisposedAt { get; private set; }

    public ICollection<CafeAssetPhoto> Photos { get; private set; } = [];

    /// <summary>Book value of this asset line = unit price × count.</summary>
    public decimal TotalValue => UnitPrice * Quantity;

    public CafeAssetPhoto? PrimaryPhoto =>
        Photos.FirstOrDefault(photo => photo.IsPrimary) ?? Photos.FirstOrDefault();

    public void Update(
        string persianName,
        string? englishName,
        string category,
        string? serialNumber,
        string location,
        DateOnly? purchaseDate,
        decimal unitPrice,
        int quantity,
        string? supplierName,
        string? note)
    {
        SetName(persianName);
        SetCategory(category);
        SetLocation(location);
        SetUnitPrice(unitPrice);
        SetQuantity(quantity);

        EnglishName = string.IsNullOrWhiteSpace(englishName) ? null : englishName.Trim();
        SerialNumber = string.IsNullOrWhiteSpace(serialNumber) ? null : serialNumber.Trim();
        SupplierName = string.IsNullOrWhiteSpace(supplierName) ? null : supplierName.Trim();
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        PurchaseDate = purchaseDate;
    }

    /// <summary>Takes the asset out of service (خارج از رده) with a reason.</summary>
    public void Retire(string? disposalNote)
    {
        if (!IsActive)
        {
            return;
        }

        IsActive = false;
        DisposeDNote(disposalNote);
    }

    /// <summary>Brings the asset back into service after a wrong retirement.</summary>
    public void Restore()
    {
        if (IsActive)
        {
            return;
        }

        IsActive = true;
        DisposalNote = null;
        DisposedAt = null;
    }

    private void DisposeDNote(string? disposalNote)
    {
        DisposalNote = string.IsNullOrWhiteSpace(disposalNote) ? null : disposalNote.Trim();
        DisposedAt = DateTimeOffset.UtcNow;
    }

    private void SetName(string persianName)
    {
        if (string.IsNullOrWhiteSpace(persianName))
        {
            throw new DomainException("نام فارسی اموال الزامی است.");
        }

        PersianName = persianName.Trim();
    }

    private void SetCategory(string category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            throw new DomainException("دسته‌بندی اموال الزامی است.");
        }

        Category = category.Trim();
    }

    private void SetLocation(string location)
    {
        if (string.IsNullOrWhiteSpace(location))
        {
            throw new DomainException("محل استقرار الزامی است.");
        }

        Location = location.Trim();
    }

    private void SetUnitPrice(decimal unitPrice)
    {
        if (unitPrice < 0)
        {
            throw new DomainException("قیمت واحد نمی‌تواند منفی باشد.");
        }

        UnitPrice = unitPrice;
    }

    private void SetQuantity(int quantity)
    {
        if (quantity <= 0)
        {
            throw new DomainException("تعداد باید حداقل ۱ باشد.");
        }

        Quantity = quantity;
    }
}
