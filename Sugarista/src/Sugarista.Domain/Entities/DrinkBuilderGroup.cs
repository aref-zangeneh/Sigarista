using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

/// <summary>
/// A logical group of builder options (for example "شیر", "سیروپ", "میزان یخ").
/// A group is either a slider group (each option has a min/max amount) or a
/// choice group (each option is either picked or not).
/// </summary>
public class DrinkBuilderGroup : BaseEntity
{
    private DrinkBuilderGroup()
    {
    }

    public DrinkBuilderGroup(
        Guid profileId,
        string name,
        DrinkBuilderInputType inputType,
        DrinkBuilderSelectionMode selectionMode,
        int minSelections,
        int maxSelections,
        bool isRequired,
        int displayOrder)
    {
        if (profileId == Guid.Empty)
        {
            throw new DomainException("گروه نوشیدنی باید به یک پروفایل متصل باشد.");
        }

        ProfileId = profileId;
        Update(name, inputType, selectionMode, minSelections, maxSelections, isRequired, displayOrder);
    }

    public Guid ProfileId { get; private set; }

    public DrinkBuilderProfile Profile { get; private set; } = null!;

    public string Name { get; private set; } = string.Empty;

    public DrinkBuilderInputType InputType { get; private set; }

    public DrinkBuilderSelectionMode SelectionMode { get; private set; }

    public int MinSelections { get; private set; }

    public int MaxSelections { get; private set; }

    public bool IsRequired { get; private set; }

    public int DisplayOrder { get; private set; }

    public ICollection<DrinkBuilderOption> Options { get; private set; } = [];

    public void Update(
        string name,
        DrinkBuilderInputType inputType,
        DrinkBuilderSelectionMode selectionMode,
        int minSelections,
        int maxSelections,
        bool isRequired,
        int displayOrder)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("نام گروه نوشیدنی الزامی است.");
        }

        if (displayOrder < 0)
        {
            throw new DomainException("ترتیب نمایش گروه نمی‌تواند منفی باشد.");
        }

        if (selectionMode == DrinkBuilderSelectionMode.Single)
        {
            // A single-choice group can never accept more than one option.
            maxSelections = 1;
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

        Name = name.Trim();
        InputType = inputType;
        SelectionMode = selectionMode;
        MinSelections = minSelections;
        MaxSelections = maxSelections;
        IsRequired = isRequired || minSelections > 0;
        DisplayOrder = displayOrder;
    }

    public DrinkBuilderOption AddOption(
        string name,
        string? englishName,
        DrinkBuilderUnit unit,
        decimal pricePerUnit,
        decimal minAmount,
        decimal maxAmount,
        decimal step,
        decimal defaultAmount,
        bool isDefault,
        bool isActive,
        int displayOrder)
    {
        var option = new DrinkBuilderOption(
            Id,
            name,
            englishName,
            unit,
            pricePerUnit,
            minAmount,
            maxAmount,
            step,
            defaultAmount,
            isDefault,
            isActive,
            displayOrder);

        Options.Add(option);
        return option;
    }

    public void RemoveOption(DrinkBuilderOption option) => Options.Remove(option);
}
