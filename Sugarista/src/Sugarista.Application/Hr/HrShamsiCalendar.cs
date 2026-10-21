using System.Globalization;

namespace Sugarista.Application.Hr;

/// <summary>
/// Shamsi (Persian calendar) helpers for payroll periods and labels.
/// </summary>
public static class HrShamsiCalendar
{
    public static readonly PersianCalendar Calendar = new();

    /// <summary>All 12 Shamsi month names (فروردین .. اسفند).</summary>
    public static readonly string[] MonthNames =
    [
        "فروردین", "اردیبهشت", "خرداد", "تیر", "مرداد", "شهریور",
        "مهر", "آبان", "آذر", "دی", "بهمن", "اسفند",
    ];

    public static (DateOnly StartDate, DateOnly EndDate) MonthRange(int shamsiYear, int shamsiMonth)
    {
        var start = Calendar.ToDateTime(shamsiYear, shamsiMonth, 1, 0, 0, 0, 0);
        var daysInMonth = Calendar.GetDaysInMonth(shamsiYear, shamsiMonth);
        var end = Calendar.ToDateTime(shamsiYear, shamsiMonth, daysInMonth, 23, 59, 59, 0);
        return (DateOnly.FromDateTime(start), DateOnly.FromDateTime(end));
    }

    public static (int Year, int Month) CurrentShamsi(DateTimeOffset now)
    {
        return (Calendar.GetYear(now.DateTime), Calendar.GetMonth(now.DateTime));
    }

    public static string MonthLabel(int shamsiYear, int shamsiMonth)
    {
        return $"{MonthNames[Math.Clamp(shamsiMonth, 1, 12) - 1]} {shamsiYear:0000}";
    }
}
