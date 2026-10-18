using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

public class IngredientSupplier : BaseAuditableEntity
{
    private IngredientSupplier()
    {
    }

    public IngredientSupplier(Guid ingredientId, Guid supplierId, bool isPreferred = false)
    {
        if (ingredientId == Guid.Empty)
        {
            throw new DomainException("Ingredient is required.");
        }

        if (supplierId == Guid.Empty)
        {
            throw new DomainException("Supplier is required.");
        }

        IngredientId = ingredientId;
        SupplierId = supplierId;
        IsPreferred = isPreferred;
    }

    public Guid IngredientId { get; private set; }

    public Ingredient Ingredient { get; private set; } = null!;

    public Guid SupplierId { get; private set; }

    public Supplier Supplier { get; private set; } = null!;

    public bool IsPreferred { get; private set; }

    public void SetPreferred(bool isPreferred)
    {
        IsPreferred = isPreferred;
    }
}
