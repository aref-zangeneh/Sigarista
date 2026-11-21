using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

/// <summary>
/// One option choice inside an <see cref="OptionGroupTemplate"/>.
/// </summary>
public class OptionGroupTemplateItem : BaseEntity
{
    private OptionGroupTemplateItem()
    {
    }

    public OptionGroupTemplateItem(
        Guid templateId,
        string persianName,
        string? englishName,
        decimal priceDelta,
        bool isDefault,
        bool isActive,
        int displayOrder)
    {
        if (templateId == Guid.Empty)
        {
            throw new DomainException("گزینهٔ قالب باید به یک قالب گروه آپشن متصل باشد.");
        }

        TemplateId = templateId;
        Update(persianName, englishName, priceDelta, isDefault, isActive, displayOrder);
    }

    public Guid TemplateId { get; private set; }

    public OptionGroupTemplate Template { get; private set; } = null!;

    public string PersianName { get; private set; } = string.Empty;

    public string? EnglishName { get; private set; }

    public decimal PriceDelta { get; private set; }

    public bool IsDefault { get; private set; }

    public bool IsActive { get; private set; } = true;

    public int DisplayOrder { get; private set; }

    public ICollection<OptionGroupTemplateItemIngredient> Ingredients { get; private set; } = [];

    public void Update(
        string persianName,
        string? englishName,
        decimal priceDelta,
        bool isDefault,
        bool isActive,
        int displayOrder)
    {
        if (string.IsNullOrWhiteSpace(persianName))
        {
            throw new DomainException("نام گزینهٔ قالب آپشن الزامی است.");
        }

        if (priceDelta < 0)
        {
            throw new DomainException("مبلغ افزودهٔ گزینه نمی‌تواند منفی باشد.");
        }

        if (displayOrder < 0)
        {
            throw new DomainException("ترتیب نمایش گزینه نمی‌تواند منفی باشد.");
        }

        PersianName = persianName.Trim();
        EnglishName = string.IsNullOrWhiteSpace(englishName) ? null : englishName.Trim();
        PriceDelta = priceDelta;
        IsDefault = isDefault;
        IsActive = isActive;
        DisplayOrder = displayOrder;
    }
}
