using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

/// <summary>
/// Per-category commission rate override (e.g., drinks 12% vs cakes 6%).
/// </summary>
public class CategoryCommission : BaseAuditableEntity
{
    private CategoryCommission()
    {
    }

    public CategoryCommission(Guid categoryId, decimal commissionPercentage)
    {
        if (categoryId == Guid.Empty)
        {
            throw new DomainException("شناسه دسته‌بندی الزامی است.");
        }

        CategoryId = categoryId;
        SetCommissionPercentage(commissionPercentage);
    }

    public Guid CategoryId { get; private set; }

    public Category Category { get; private set; } = null!;

    public decimal CommissionPercentage { get; private set; }

    public void SetCommissionPercentage(decimal commissionPercentage)
    {
        if (commissionPercentage < 0m || commissionPercentage > 100m)
        {
            throw new DomainException("درصد پورسانت دسته‌بندی باید بین ۰ تا ۱۰۰ باشد.");
        }

        CommissionPercentage = commissionPercentage;
    }
}
