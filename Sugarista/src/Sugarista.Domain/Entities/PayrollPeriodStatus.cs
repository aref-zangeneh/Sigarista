namespace Sugarista.Domain.Entities;

public enum PayrollPeriodStatus
{
    /// <summary>پیش‌نویس — قابل ویرایش</summary>
    Draft = 0,

    /// <summary>قطعی‌شده — فقط پرداخت امکان دارد</summary>
    Finalized = 1,

    /// <summary>کامل پرداخت‌شده</summary>
    FullyPaid = 2,
}
