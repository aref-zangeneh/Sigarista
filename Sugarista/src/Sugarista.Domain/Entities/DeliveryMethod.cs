using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

public class DeliveryMethod : BaseAuditableEntity
{
    private readonly List<DeliveryMethodPackaging> _packagings = [];

    public DeliveryMethod()
    {
    }

    public DeliveryMethod(
        string persianName,
        string englishName,
        decimal deliveryFee,
        string? description,
        int displayOrder,
        bool isActive)
    {
        ApplyBasicFields(persianName, englishName, deliveryFee, description, displayOrder, isActive);
    }

    public string PersianName { get; private set; } = string.Empty;

    public string EnglishName { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    /// <summary>Delivery/service fee (Toman) charged on top of the order total for this method.</summary>
    public decimal DeliveryFee { get; private set; }

    public int DisplayOrder { get; private set; }

    public bool IsActive { get; private set; } = true;

    public IReadOnlyCollection<DeliveryMethodPackaging> Packagings => _packagings.AsReadOnly();

    public void Update(
        string persianName,
        string englishName,
        decimal deliveryFee,
        string? description,
        int displayOrder,
        bool isActive)
    {
        ApplyBasicFields(persianName, englishName, deliveryFee, description, displayOrder, isActive);
    }

    private void ApplyBasicFields(
        string persianName,
        string englishName,
        decimal deliveryFee,
        string? description,
        int displayOrder,
        bool isActive)
    {
        if (string.IsNullOrWhiteSpace(persianName))
        {
            throw new DomainException("Delivery method persian name is required.");
        }

        if (string.IsNullOrWhiteSpace(englishName))
        {
            throw new DomainException("Delivery method english name is required.");
        }

        if (deliveryFee < 0)
        {
            throw new DomainException("Delivery fee cannot be negative.");
        }

        PersianName = persianName.Trim();
        EnglishName = englishName.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        DeliveryFee = deliveryFee;
        DisplayOrder = displayOrder;
        IsActive = isActive;
    }
}
