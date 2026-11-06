using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

/// <summary>
/// One priced component of a custom drink. Slider options are charged
/// <c>PricePerUnit × Amount</c>; choice options are charged their flat
/// <c>PricePerUnit</c> once selected.
/// </summary>
public class DrinkBuilderOption : BaseEntity
{
    private DrinkBuilderOption()
    {
    }

    public DrinkBuilderOption(
        Guid groupId,
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
        if (groupId == Guid.Empty)
        {
            throw new DomainException("گزینهٔ نوشیدنی باید به یک گروه متصل باشد.");
        }

        GroupId = groupId;
        Update(
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
    }

    public Guid GroupId { get; private set; }

    public DrinkBuilderGroup Group { get; private set; } = null!;

    public string Name { get; private set; } = string.Empty;

    public string? EnglishName { get; private set; }

    public DrinkBuilderUnit Unit { get; private set; }

    public decimal PricePerUnit { get; private set; }

    public decimal MinAmount { get; private set; }

    public decimal MaxAmount { get; private set; }

    public decimal Step { get; private set; }

    public decimal DefaultAmount { get; private set; }

    public bool IsDefault { get; private set; }

    public bool IsActive { get; private set; }

    public int DisplayOrder { get; private set; }

    public void Update(
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
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("نام گزینهٔ نوشیدنی الزامی است.");
        }

        if (!Enum.IsDefined(unit))
        {
            throw new DomainException("واحد اندازه‌گیری گزینه معتبر نیست.");
        }

        if (pricePerUnit < 0)
        {
            throw new DomainException("قیمت هر واحد نمی‌تواند منفی باشد.");
        }

        if (minAmount < 0)
        {
            throw new DomainException("حداقل مقدار نمی‌تواند منفی باشد.");
        }

        if (maxAmount < minAmount)
        {
            throw new DomainException("حداکثر مقدار نمی‌تواند از حداقل کمتر باشد.");
        }

        if (step <= 0)
        {
            throw new DomainException("گام تغییر مقدار باید بزرگ‌تر از صفر باشد.");
        }

        // Zero always means "not included", even when the option has a minimum
        // amount. The customer opts in explicitly through the slider/toggle.
        if (defaultAmount != 0m && (defaultAmount < minAmount || defaultAmount > maxAmount))
        {
            throw new DomainException("مقدار پیش‌فرض باید بین حداقل و حداکثر مقدار باشد.");
        }

        if (defaultAmount > 0 && !IsAligned(defaultAmount, minAmount, step))
        {
            throw new DomainException("مقدار پیش‌فرض باید با گام تغییر مقدار هم‌تراز باشد.");
        }

        if (isDefault && defaultAmount <= 0)
        {
            throw new DomainException("گزینهٔ پیش‌فرض باید مقدار پیش‌فرض بزرگ‌تر از صفر داشته باشد.");
        }

        if (displayOrder < 0)
        {
            throw new DomainException("ترتیب نمایش گزینه نمی‌تواند منفی باشد.");
        }

        Name = name.Trim();
        EnglishName = string.IsNullOrWhiteSpace(englishName) ? null : englishName.Trim();
        Unit = unit;
        PricePerUnit = pricePerUnit;
        MinAmount = minAmount;
        MaxAmount = maxAmount;
        Step = step;
        DefaultAmount = defaultAmount;
        IsDefault = isDefault;
        IsActive = isActive;
        DisplayOrder = displayOrder;
    }

    public void SetActive(bool isActive) => IsActive = isActive;

    /// <summary>True when <paramref name="amount"/> sits on the min + n × step grid.</summary>
    public static bool IsAligned(decimal amount, decimal minAmount, decimal step)
    {
        // Zero means "not included" and never needs to sit on the grid.
        if (amount == 0m)
        {
            return false;
        }

        if (step <= 0)
        {
            return true;
        }

        var remainder = (amount - minAmount) % step;
        return Math.Abs(remainder) < 0.0001m || Math.Abs(Math.Abs(remainder) - step) < 0.0001m;
    }
}
