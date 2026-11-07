using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

/// <summary>
/// One priced choice inside a <see cref="MenuItemOptionGroup"/> — for example
/// "۱۰۰٪ عربیکا (+۱۲۰٬۰۰۰)" or "۳۰ میل سیروپ فندوق (+۵۰٬۰۰۰)".
/// The exact inventory usage of the option is defined through its
/// <see cref="MenuItemOptionIngredient"/> rows: selecting the option deducts
/// those ingredient amounts from stock, exactly like a mini recipe.
/// </summary>
public class MenuItemOption : BaseEntity
{
    private MenuItemOption()
    {
    }

    public MenuItemOption(
        Guid groupId,
        string persianName,
        string? englishName,
        decimal priceDelta,
        bool isDefault,
        bool isActive,
        int displayOrder)
    {
        if (groupId == Guid.Empty)
        {
            throw new DomainException("گزینهٔ آپشن باید به یک گروه متصل باشد.");
        }

        GroupId = groupId;
        Update(persianName, englishName, priceDelta, isDefault, isActive, displayOrder);
    }

    public Guid GroupId { get; private set; }

    public MenuItemOptionGroup Group { get; private set; } = null!;

    public string PersianName { get; private set; } = string.Empty;

    public string? EnglishName { get; private set; }

    /// <summary>Amount added to the item's base price when the customer picks this option.</summary>
    public decimal PriceDelta { get; private set; }

    /// <summary>Pre-selected in the popup for single-choice groups (customer still sees it explicitly).</summary>
    public bool IsDefault { get; private set; }

    public bool IsActive { get; private set; } = true;

    public int DisplayOrder { get; private set; }

    public ICollection<MenuItemOptionIngredient> Ingredients { get; private set; } = [];

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
            throw new DomainException("نام گزینهٔ آپشن الزامی است.");
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

    public void SetActive(bool isActive) => IsActive = isActive;
}
