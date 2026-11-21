using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

/// <summary>
/// One ingredient consumption row belonging to an <see cref="OptionGroupTemplateItem"/>.
/// Copied automatically into <see cref="MenuItemOptionIngredient"/> whenever the
/// template is attached to a menu item.
/// </summary>
public class OptionGroupTemplateItemIngredient : BaseEntity
{
    private OptionGroupTemplateItemIngredient()
    {
    }

    public OptionGroupTemplateItemIngredient(Guid templateItemId, Guid ingredientId, decimal quantity)
    {
        if (templateItemId == Guid.Empty)
        {
            throw new DomainException("قلم مصرفی گزینهٔ قالب باید به یک گزینه متصل باشد.");
        }

        if (ingredientId == Guid.Empty)
        {
            throw new DomainException("ماده اولیهٔ گزینهٔ آپشن الزامی است.");
        }

        if (quantity <= 0)
        {
            throw new DomainException("مقدار مصرف گزینهٔ آپشن باید بزرگ‌تر از صفر باشد.");
        }

        TemplateItemId = templateItemId;
        IngredientId = ingredientId;
        Quantity = quantity;
    }

    public Guid TemplateItemId { get; private set; }

    public OptionGroupTemplateItem TemplateItem { get; private set; } = null!;

    public Guid IngredientId { get; private set; }

    public Ingredient Ingredient { get; private set; } = null!;

    public decimal Quantity { get; private set; }
}
