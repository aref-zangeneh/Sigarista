using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

public class Supplier : BaseAuditableEntity
{
    public Supplier()
    {
    }

    public Supplier(
        string persianName,
        string phoneNumber,
        string contactName,
        string notes,
        int displayOrder,
        bool isActive = true)
    {
        Update(persianName, phoneNumber, contactName, notes, displayOrder, isActive);
    }

    public string PersianName { get; private set; } = string.Empty;

    public string PhoneNumber { get; private set; } = string.Empty;

    public string ContactName { get; private set; } = string.Empty;

    public string Notes { get; private set; } = string.Empty;

    public int DisplayOrder { get; private set; }

    public bool IsActive { get; private set; } = true;

    public ICollection<IngredientSupplier> IngredientSuppliers { get; private set; } = [];

    public void Update(
        string persianName,
        string phoneNumber,
        string contactName,
        string notes,
        int displayOrder,
        bool isActive)
    {
        if (string.IsNullOrWhiteSpace(persianName))
        {
            throw new DomainException("Supplier name is required.");
        }

        if (persianName.Trim().Length > 200)
        {
            throw new DomainException("Supplier name is too long.");
        }

        PersianName = persianName.Trim();
        PhoneNumber = (phoneNumber ?? string.Empty).Trim();
        ContactName = (contactName ?? string.Empty).Trim();
        Notes = (notes ?? string.Empty).Trim();
        DisplayOrder = displayOrder < 0 ? 0 : displayOrder;
        IsActive = isActive;
    }
}
