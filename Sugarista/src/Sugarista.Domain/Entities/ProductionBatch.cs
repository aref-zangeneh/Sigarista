using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

public class ProductionBatch : BaseAuditableEntity
{
    private ProductionBatch()
    {
    }

    public ProductionBatch(Guid menuItemId, decimal quantityProduced, string? note)
    {
        if (menuItemId == Guid.Empty)
        {
            throw new DomainException("Menu item is required.");
        }

        if (quantityProduced <= 0)
        {
            throw new DomainException("Production quantity must be greater than zero.");
        }

        MenuItemId = menuItemId;
        QuantityProduced = quantityProduced;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        ProducedAt = DateTimeOffset.UtcNow;
    }

    public Guid MenuItemId { get; private set; }

    public MenuItem MenuItem { get; private set; } = null!;

    public decimal QuantityProduced { get; private set; }

    public string? Note { get; private set; }

    public DateTimeOffset ProducedAt { get; private set; }
}
