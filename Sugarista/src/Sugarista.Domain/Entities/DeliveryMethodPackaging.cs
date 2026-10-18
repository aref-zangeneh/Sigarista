using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

/// <summary>
/// One packaging line of a delivery method's disposable set: every order served with
/// this delivery method automatically consumes QuantityPerOrder units of the packaging item.
/// </summary>
public class DeliveryMethodPackaging : BaseAuditableEntity
{
    private DeliveryMethodPackaging()
    {
    }

    public DeliveryMethodPackaging(Guid deliveryMethodId, Guid ingredientId, decimal quantityPerOrder)
    {
        if (deliveryMethodId == Guid.Empty)
        {
            throw new DomainException("Delivery method is required.");
        }

        if (ingredientId == Guid.Empty)
        {
            throw new DomainException("Packaging item is required.");
        }

        if (quantityPerOrder <= 0)
        {
            throw new DomainException("Packaging quantity per order must be greater than zero.");
        }

        DeliveryMethodId = deliveryMethodId;
        IngredientId = ingredientId;
        QuantityPerOrder = quantityPerOrder;
    }

    public Guid DeliveryMethodId { get; private set; }

    public DeliveryMethod DeliveryMethod { get; private set; } = null!;

    public Guid IngredientId { get; private set; }

    public Ingredient Ingredient { get; private set; } = null!;

    public decimal QuantityPerOrder { get; private set; }

    public void UpdateQuantity(decimal quantityPerOrder)
    {
        if (quantityPerOrder <= 0)
        {
            throw new DomainException("Packaging quantity per order must be greater than zero.");
        }

        QuantityPerOrder = quantityPerOrder;
    }
}
