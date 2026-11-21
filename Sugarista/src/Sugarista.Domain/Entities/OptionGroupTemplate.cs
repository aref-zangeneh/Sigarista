using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

/// <summary>
/// A reusable option-group template (e.g. "نوع شیر", "سیروپ‌ها", "دمای سرو")
/// organized optionally by <see cref="CategoryLabel"/> so admins can define
/// option groups once in a central library and attach or sync them onto any
/// number of menu items alongside custom per-item groups.
/// </summary>
public class OptionGroupTemplate : BaseAuditableEntity
{
    private OptionGroupTemplate()
    {
    }

    public OptionGroupTemplate(
        string? categoryLabel,
        string persianName,
        string? englishName,
        bool isRequired,
        string selectionMode,
        int minSelections,
        int maxSelections,
        int displayOrder,
        bool isActive = true)
    {
        Update(categoryLabel, persianName, englishName, isRequired, selectionMode, minSelections, maxSelections, displayOrder, isActive);
    }

    /// <summary>Optional admin-facing grouping label (e.g. "نوشیدنی‌های بر پایه شیر", "سیروپ‌ها").</summary>
    public string? CategoryLabel { get; private set; }

    public string PersianName { get; private set; } = string.Empty;

    public string? EnglishName { get; private set; }

    public bool IsRequired { get; private set; }

    public string SelectionMode { get; private set; } = MenuItemOptionGroup.SingleMode;

    public int MinSelections { get; private set; }

    public int MaxSelections { get; private set; }

    public int DisplayOrder { get; private set; }

    public bool IsActive { get; private set; } = true;

    public ICollection<OptionGroupTemplateItem> Options { get; private set; } = [];

    public void Update(
        string? categoryLabel,
        string persianName,
        string? englishName,
        bool isRequired,
        string selectionMode,
        int minSelections,
        int maxSelections,
        int displayOrder,
        bool isActive = true)
    {
        if (string.IsNullOrWhiteSpace(persianName))
        {
            throw new DomainException("نام قالب گروه آپشن الزامی است.");
        }

        var normalizedMode = MenuItemOptionGroup.NormalizeMode(selectionMode);

        if (displayOrder < 0)
        {
            throw new DomainException("ترتیب نمایش قالب گروه آپشن نمی‌تواند منفی باشد.");
        }

        if (minSelections < 0)
        {
            throw new DomainException("حداقل تعداد انتخاب نمی‌تواند منفی باشد.");
        }

        if (maxSelections < 1)
        {
            throw new DomainException("حداکثر تعداد انتخاب باید حداقل ۱ باشد.");
        }

        if (minSelections > maxSelections)
        {
            throw new DomainException("حداقل تعداد انتخاب نمی‌تواند از حداکثر بیشتر باشد.");
        }

        if (isRequired && minSelections == 0)
        {
            minSelections = 1;
        }
        else if (!isRequired && minSelections > 0)
        {
            minSelections = 0;
        }

        if (normalizedMode == MenuItemOptionGroup.SingleMode)
        {
            maxSelections = 1;
            if (minSelections > 1)
            {
                minSelections = 1;
            }
        }

        var trimmedCategory = string.IsNullOrWhiteSpace(categoryLabel) ? null : categoryLabel.Trim();
        if (trimmedCategory is { Length: > 100 })
        {
            throw new DomainException("نام دسته‌بندی قالب حداکثر ۱۰۰ کاراکتر می‌تواند باشد.");
        }

        CategoryLabel = trimmedCategory;
        PersianName = persianName.Trim();
        EnglishName = string.IsNullOrWhiteSpace(englishName) ? null : englishName.Trim();
        IsRequired = isRequired;
        SelectionMode = normalizedMode;
        MinSelections = minSelections;
        MaxSelections = maxSelections;
        DisplayOrder = displayOrder;
        IsActive = isActive;
    }

    public OptionGroupTemplateItem AddOption(
        string persianName,
        string? englishName,
        decimal priceDelta,
        bool isDefault,
        bool isActive,
        int displayOrder)
    {
        var option = new OptionGroupTemplateItem(
            Id,
            persianName,
            englishName,
            priceDelta,
            isDefault,
            isActive,
            displayOrder);

        Options.Add(option);
        return option;
    }
}
