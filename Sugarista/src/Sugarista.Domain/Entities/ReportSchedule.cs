using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

public enum ReportFrequency
{
    Daily = 0,
    Weekly = 1,
    Monthly = 2,
}

/// <summary>
/// A recurring report definition. The background runner checks schedules every
/// minute, builds the Excel workbook for the last period and emails it to the
/// configured recipients. All fields are editable at runtime — no restart.
/// </summary>
public class ReportSchedule : BaseAuditableEntity
{
    private ReportSchedule()
    {
    }

    public ReportSchedule(
        string name,
        string kind,
        ReportFrequency frequency,
        int hour,
        string recipientEmails,
        string? optionsJson = null,
        int? dayOfWeek = null,
        int? dayOfMonth = null,
        bool isEnabled = true)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("A report schedule needs a name.");
        }

        if (string.IsNullOrWhiteSpace(kind))
        {
            throw new DomainException("A report schedule needs a report kind.");
        }

        if (string.IsNullOrWhiteSpace(recipientEmails))
        {
            throw new DomainException("یک ایمیل دریافت‌کننده برای گزارش دوره‌ای لازم است.");
        }

        if (hour is < 0 or > 23)
        {
            throw new DomainException("ساعت اجرای گزارش باید بین ۰ تا ۲۳ باشد.");
        }

        Name = name.Trim();
        Kind = kind.Trim().ToLowerInvariant();
        Frequency = frequency;
        Hour = hour;
        RecipientEmails = recipientEmails.Trim();
        OptionsJson = string.IsNullOrWhiteSpace(optionsJson) ? null : optionsJson;
        DayOfWeek = frequency == ReportFrequency.Weekly ? Math.Clamp(dayOfWeek ?? 0, 0, 6) : null;
        DayOfMonth = frequency == ReportFrequency.Monthly ? Math.Clamp(dayOfMonth ?? 1, 1, 31) : null;
        IsEnabled = isEnabled;
    }

    public string Name { get; private set; } = string.Empty;

    /// <summary>Report kind (currently <c>discount-report</c>).</summary>
    public string Kind { get; private set; } = string.Empty;

    public ReportFrequency Frequency { get; private set; }

    /// <summary>Local hour (0-23) at which the report is sent.</summary>
    public int Hour { get; private set; }

    /// <summary>Weekly runs: 0 = Sunday … 6 = Saturday.</summary>
    public int? DayOfWeek { get; private set; }

    /// <summary>Monthly runs: 1-31 (clamped to the last day of shorter months).</summary>
    public int? DayOfMonth { get; private set; }

    /// <summary>Comma-separated recipient addresses.</summary>
    public string RecipientEmails { get; private set; } = string.Empty;

    /// <summary>Serialized report options (selected columns / filters).</summary>
    public string? OptionsJson { get; private set; }

    public bool IsEnabled { get; private set; }

    public DateTimeOffset? LastRunAt { get; private set; }

    public void Update(
        string name,
        string kind,
        ReportFrequency frequency,
        int hour,
        string recipientEmails,
        string? optionsJson,
        int? dayOfWeek,
        int? dayOfMonth,
        bool isEnabled)
    {
        var updated = new ReportSchedule(
            name, kind, frequency, hour, recipientEmails, optionsJson, dayOfWeek, dayOfMonth, isEnabled);
        Name = updated.Name;
        Kind = updated.Kind;
        Frequency = updated.Frequency;
        Hour = updated.Hour;
        RecipientEmails = updated.RecipientEmails;
        OptionsJson = updated.OptionsJson;
        DayOfWeek = updated.DayOfWeek;
        DayOfMonth = updated.DayOfMonth;
        IsEnabled = updated.IsEnabled;
    }

    public IReadOnlyCollection<string> Recipients() =>
        RecipientEmails
            .Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>
    /// Whether the schedule should fire at <paramref name="localNow"/>. Runs at
    /// most once per period: a daily report once per local day, weekly once per
    /// week, monthly once per month.
    /// </summary>
    public bool IsDue(DateTimeOffset localNow)
    {
        if (!IsEnabled || localNow.Hour != Hour)
        {
            return false;
        }

        if (Frequency == ReportFrequency.Weekly && localNow.DayOfWeek != (DayOfWeek)(DayOfWeek ?? 0))
        {
            return false;
        }

        if (Frequency == ReportFrequency.Monthly)
        {
            var lastDay = DateTime.DaysInMonth(localNow.Year, localNow.Month);
            var target = Math.Min(DayOfMonth ?? 1, lastDay);
            if (localNow.Day != target)
            {
                return false;
            }
        }

        if (LastRunAt is null)
        {
            return true;
        }

        var lastLocal = LastRunAt.Value.ToOffset(localNow.Offset);
        return Frequency switch
        {
            ReportFrequency.Daily => lastLocal.Date < localNow.Date,
            ReportFrequency.Weekly => lastLocal.Date < localNow.Date.AddDays(-6),
            ReportFrequency.Monthly => lastLocal.Year < localNow.Year || lastLocal.Month < localNow.Month,
            _ => false,
        };
    }

    public void MarkRun(DateTimeOffset now) => LastRunAt = now;
}
