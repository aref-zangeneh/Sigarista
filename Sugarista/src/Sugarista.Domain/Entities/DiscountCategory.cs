using Sugarista.Domain.Common;

namespace Sugarista.Domain.Entities;

/// <summary>A category a discount is limited to. No rows means every category qualifies.</summary>
public class DiscountCategory : BaseEntity
{
    private DiscountCategory()
    {
    }

    public DiscountCategory(Guid discountId, Guid categoryId)
    {
        DiscountId = discountId;
        CategoryId = categoryId;
    }

    public Guid DiscountId { get; private set; }

    public Discount Discount { get; private set; } = null!;

    public Guid CategoryId { get; private set; }

    public Category Category { get; private set; } = null!;
}
