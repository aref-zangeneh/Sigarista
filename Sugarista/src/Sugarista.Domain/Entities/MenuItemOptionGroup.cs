using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

/// <summary>
/// A per-item option group rendered as one section of the customer's
/// add-to-cart popup — for example "درصد دانه قهوه".
/// <see cref="IsRequired"/> marks mandatory sections ("اجباری"), everything
/// else is optional ("دلخواه").
/// </summary>
public class MenuItemOptionGroup : BaseEntity
{
    public const string SingleMode = "Single";
    public const string MultiMode = "Multi";

    private MenuItemOptionGroup()
    {
    }

    public MenuItemOptionGroup(
        Guid menuItemId,
        string persianName,
        string? englishName,
        bool isRequired,
        string selectionMode,
        int minSelections,
        int maxSelections,
        int displayOrder,
        bool isActive = true)
    {
        if (menuItemId == Guid.Empty)
        {
            throw new DomainException("گروه آپشن باید به یک آیتم منو متصل باشد.");
        }

        MenuItemId = menuItemId;
        Update(persianName, englishName, isRequired, selectionMode, minSelections, maxSelections, displayOrder, isActive);
    }

    public Guid MenuItemId { get; private set; }

    public MenuItem MenuItem { get; private set; } = null!;

    public string PersianName { get; private set; } = string.Empty;

    public string? EnglishName { get; private set; }

    /// <summary>True when the customer cannot complete the order without choosing from this group.</summary>
    public bool IsRequired { get; private set; }

    /// <summary>"Single" = radio-like (exactly one), "Multi" = toggle chips (min/max).</summary>
    public string SelectionMode { get; private set; } = SingleMode;

    public int MinSelections { get; private set; }

    public int MaxSelections { get; private set; }

    public int DisplayOrder { get; private set; }

    public bool IsActive { get; private set; } = true;

    public ICollection<MenuItemOption> Options { get; private set; } = [];

    public void Update(
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
            throw new DomainException("نام گروه آپشن الزامی است.");
        }

        var normalizedMode = NormalizeMode(selectionMode);

        if (displayOrder < 0)
        {
            throw new DomainException("ترتیب نمایش گروه آپشن نمی‌تواند منفی باشد.");
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
            // A required group always demands at least one pick.
            minSelections = 1;
        }

        if (isRequired && normalizedMode == SingleMode)
        {
            maxSelections = 1;
        }

        PersianName = persianName.Trim();
        EnglishName = string.IsNullOrWhiteSpace(englishName) ? null : englishName.Trim();
        IsRequired = isRequired || minSelections > 0;
        SelectionMode = normalizedMode;
        MinSelections = minSelections;
        MaxSelections = maxSelections;
        DisplayOrder = displayOrder;
        IsActive = isActive;
    }

    public MenuItemOption AddOption(
        string persianName,
        string? englishName,
        decimal priceDelta,
        bool isDefault,
        bool isActive,
        int displayOrder)
    {
        var option = new MenuItemOption(
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

    public void RemoveOption(MenuItemOption option) => Options.Remove(option);

    public static string NormalizeMode(string? selectionMode) =>
        string.Equals(selectionMode, MultiMode, StringComparison.OrdinalIgnoreCase)
            ? MultiMode
            : SingleMode;
}
