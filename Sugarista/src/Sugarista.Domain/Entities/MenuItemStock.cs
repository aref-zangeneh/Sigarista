using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

public class MenuItemStock : BaseAuditableEntity
{
    private MenuItemStock()
    {
    }

    public MenuItemStock(Guid menuItemId, decimal minAlertQuantity = 0)
    {
        if (menuItemId == Guid.Empty)
        {
            throw new DomainException("Menu item is required.");
        }

        MenuItemId = menuItemId;
        CurrentQuantity = 0;
        MinAlertQuantity = minAlertQuantity >= 0 ? minAlertQuantity : 0;
        LastUpdatedAt = DateTimeOffset.UtcNow;
    }

    public Guid MenuItemId { get; private set; }

    public MenuItem MenuItem { get; private set; } = null!;

    public decimal CurrentQuantity { get; private set; }

    public decimal MinAlertQuantity { get; private set; }

    public DateTimeOffset LastUpdatedAt { get; private set; }

    public bool IsLowStock => CurrentQuantity < MinAlertQuantity;

    public void AddProduction(decimal quantity)
    {
        if (quantity <= 0)
        {
            throw new DomainException("Production quantity must be greater than zero.");
        }

        CurrentQuantity += quantity;
        LastUpdatedAt = DateTimeOffset.UtcNow;
    }

    public void DeductForSale(decimal quantity)
    {
        if (quantity <= 0)
        {
            throw new DomainException("Sale quantity must be greater than zero.");
        }

        CurrentQuantity -= quantity;
        LastUpdatedAt = DateTimeOffset.UtcNow;
    }

    public void RestoreFromSale(decimal quantity)
    {
        if (quantity <= 0)
        {
            throw new DomainException("Restore quantity must be greater than zero.");
        }

        CurrentQuantity += quantity;
        LastUpdatedAt = DateTimeOffset.UtcNow;
    }
}
