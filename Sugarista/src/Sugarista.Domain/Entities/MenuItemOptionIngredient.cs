using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

/// <summary>
/// One ingredient consumption row of a <see cref="MenuItemOption"/>.
/// Selecting the option on an order line deducts exactly
/// <c>Quantity × line quantity</c> of the ingredient from stock.
/// </summary>
public class MenuItemOptionIngredient : BaseEntity
{
    private MenuItemOptionIngredient()
    {
    }

    public MenuItemOptionIngredient(Guid optionId, Guid ingredientId, decimal quantity)
    {
        if (optionId == Guid.Empty)
        {
            throw new DomainException("قلم مصرفی گزینه باید به یک گزینه متصل باشد.");
        }

        if (ingredientId == Guid.Empty)
        {
            throw new DomainException("ماده اولیهٔ گزینهٔ آپشن الزامی است.");
        }

        if (quantity <= 0)
        {
            throw new DomainException("مقدار مصرف گزینهٔ آپشن باید بزرگ‌تر از صفر باشد.");
        }

        OptionId = optionId;
        IngredientId = ingredientId;
        Quantity = quantity;
    }

    public Guid OptionId { get; private set; }

    public MenuItemOption Option { get; private set; } = null!;

    public Guid IngredientId { get; private set; }

    public Ingredient Ingredient { get; private set; } = null!;

    /// <summary>Amount consumed per one unit of the option, in the ingredient's unit (ml/g/عدد…).</summary>
    public decimal Quantity { get; private set; }

    public void Update(Guid ingredientId, decimal quantity)
    {
        if (ingredientId == Guid.Empty)
        {
            throw new DomainException("ماده اولیهٔ گزینهٔ آپشن الزامی است.");
        }

        if (quantity <= 0)
        {
            throw new DomainException("مقدار مصرف گزینهٔ آپشن باید بزرگ‌تر از صفر باشد.");
        }

        IngredientId = ingredientId;
        Quantity = quantity;
    }
}
