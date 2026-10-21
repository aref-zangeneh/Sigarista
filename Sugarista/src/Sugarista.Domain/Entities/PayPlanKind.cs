namespace Sugarista.Domain.Entities;

/// <summary>
/// Pay scheme of a cafe staff member. Every scheme is a mix of fixed pay,
/// hourly/shift pay and a sales commission — each kind selects which parts apply.
/// </summary>
public enum PayPlanKind
{
    /// <summary>ثابت ماهانه</summary>
    Monthly = 0,

    /// <summary>ساعتی</summary>
    Hourly = 1,

    /// <summary>شیفتی (به‌ازای هر شیفت)</summary>
    ShiftBased = 2,

    /// <summary>فقط پورسانت درصدی از فروش</summary>
    CommissionOnly = 3,

    /// <summary>حقوق ثابت + پورسانت</summary>
    MonthlyPlusCommission = 4,

    /// <summary>ساعتی + پورسانت</summary>
    HourlyPlusCommission = 5,
}
