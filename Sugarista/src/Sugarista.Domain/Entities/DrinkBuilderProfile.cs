using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

/// <summary>How a builder group is presented to the customer.</summary>
public enum DrinkBuilderInputType
{
    /// <summary>A continuous amount (grams, millilitres, shots...) chosen with a slider.</summary>
    Slider = 0,

    /// <summary>A discrete option (milk type, sweetness level...) chosen as chips.</summary>
    Choice = 1,
}

/// <summary>How many options a group accepts.</summary>
public enum DrinkBuilderSelectionMode
{
    Single = 0,
    Multiple = 1,
}

/// <summary>Unit used by a slider option. Labels live in the application layer.</summary>
public enum DrinkBuilderUnit
{
    Gram = 0,
    Milliliter = 1,
    Piece = 2,
    Shot = 3,
    Pump = 4,
}

/// <summary>
/// A drink that can be customised by the customer. One profile belongs to one
/// menu item; the menu item's size prices form the base price and every group
/// below it adds priced components on top. Profiles are fully optional: when
/// the global feature flag is off (or no profile exists) the theme hides the
/// builder entirely.
/// </summary>
public class DrinkBuilderProfile : BaseAuditableEntity
{
    private DrinkBuilderProfile()
    {
    }

    public DrinkBuilderProfile(Guid menuItemId, string name, bool isEnabled = true, int displayOrder = 0)
    {
        if (menuItemId == Guid.Empty)
        {
            throw new DomainException("پروفایل سازندهٔ نوشیدنی باید به یک آیتم منو متصل باشد.");
        }

        MenuItemId = menuItemId;
        Update(name, isEnabled, displayOrder);
    }

    public Guid MenuItemId { get; private set; }

    public MenuItem MenuItem { get; private set; } = null!;

    public string Name { get; private set; } = string.Empty;

    public bool IsEnabled { get; private set; }

    public int DisplayOrder { get; private set; }

    public ICollection<DrinkBuilderGroup> Groups { get; private set; } = [];

    public void Update(string name, bool isEnabled, int displayOrder)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("نام پروفایل نوشیدنی الزامی است.");
        }

        if (displayOrder < 0)
        {
            throw new DomainException("ترتیب نمایش نمی‌تواند منفی باشد.");
        }

        Name = name.Trim();
        IsEnabled = isEnabled;
        DisplayOrder = displayOrder;
    }

    public void SetEnabled(bool isEnabled) => IsEnabled = isEnabled;

    public void ChangeMenuItem(Guid menuItemId)
    {
        if (menuItemId == Guid.Empty)
        {
            throw new DomainException("پروفایل سازندهٔ نوشیدنی باید به یک آیتم منو متصل باشد.");
        }

        MenuItemId = menuItemId;
    }

    public DrinkBuilderGroup AddGroup(
        string name,
        DrinkBuilderInputType inputType,
        DrinkBuilderSelectionMode selectionMode,
        int minSelections,
        int maxSelections,
        bool isRequired,
        int displayOrder)
    {
        var group = new DrinkBuilderGroup(
            Id,
            name,
            inputType,
            selectionMode,
            minSelections,
            maxSelections,
            isRequired,
            displayOrder);

        Groups.Add(group);
        return group;
    }

    public void RemoveGroup(DrinkBuilderGroup group) => Groups.Remove(group);
}
