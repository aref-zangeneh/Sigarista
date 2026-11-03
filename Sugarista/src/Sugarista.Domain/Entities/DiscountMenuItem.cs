using Sugarista.Domain.Common;

namespace Sugarista.Domain.Entities;

/// <summary>A menu item a discount is limited to. No rows = every item qualifies.</summary>
public class DiscountMenuItem : BaseEntity
{
    private DiscountMenuItem()
    {
    }

    public DiscountMenuItem(Guid discountId, Guid menuItemId)
    {
        DiscountId = discountId;
        MenuItemId = menuItemId;
    }

    public Guid DiscountId { get; private set; }

    public Discount Discount { get; private set; } = null!;

    public Guid MenuItemId { get; private set; }

    public MenuItem MenuItem { get; private set; } = null!;
}
