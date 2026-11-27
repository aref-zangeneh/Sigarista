using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

/// <summary>
/// Progressive affiliate tier (e.g. Bronze, Silver, Gold) unlocked by rolling
/// 30-day approved sales. Grants a base commission percentage and an optional
/// bonus percentage when converting earnings into cafe wallet credit.
/// </summary>
public class AffiliateTier : BaseAuditableEntity
{
    private AffiliateTier()
    {
    }

    public AffiliateTier(
        string name,
        decimal minMonthlySales,
        decimal commissionPercentage,
        decimal creditBonusPercentage = 0m)
    {
        Update(name, minMonthlySales, commissionPercentage, creditBonusPercentage);
    }

    public string Name { get; private set; } = string.Empty;

    public decimal MinMonthlySales { get; private set; }

    public decimal CommissionPercentage { get; private set; }

    public decimal CreditBonusPercentage { get; private set; }

    public ICollection<Affiliate> Affiliates { get; private set; } = [];

    public void Update(
        string name,
        decimal minMonthlySales,
        decimal commissionPercentage,
        decimal creditBonusPercentage)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("نام سطح بازاریاب الزامی است.");
        }

        if (minMonthlySales < 0m)
        {
            throw new DomainException("حداقل فروش ماهانه نمی‌تواند منفی باشد.");
        }

        if (commissionPercentage < 0m || commissionPercentage > 100m)
        {
            throw new DomainException("درصد پورسانت باید بین ۰ تا ۱۰۰ باشد.");
        }

        if (creditBonusPercentage < 0m || creditBonusPercentage > 100m)
        {
            throw new DomainException("درصد بونوس اعتبار کافه باید بین ۰ تا ۱۰۰ باشد.");
        }

        Name = name.Trim();
        MinMonthlySales = minMonthlySales;
        CommissionPercentage = commissionPercentage;
        CreditBonusPercentage = creditBonusPercentage;
    }
}
