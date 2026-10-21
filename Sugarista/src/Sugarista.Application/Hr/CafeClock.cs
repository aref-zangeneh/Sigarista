namespace Sugarista.Application.Hr;

/// <summary>
/// The café lives on Iran Standard Time (UTC+03:30). Punch timestamps are
/// stored as UTC instants and translated here whenever a café-local date or
/// person-friendly label is needed.
/// </summary>
public static class CafeClock
{
    private static readonly TimeZoneInfo IranZone = ResolveIranZone();

    private static TimeZoneInfo ResolveIranZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran");
        }
        catch (TimeZoneNotFoundException)
        {
            return CreateFixedZone();
        }
        catch (InvalidTimeZoneException)
        {
            return CreateFixedZone();
        }
    }

    private static TimeZoneInfo CreateFixedZone()
    {
        return TimeZoneInfo.CreateCustomTimeZone(
            "Iran Standard Time",
            new TimeSpan(3, 30, 0),
            "Iran Standard Time",
            "Iran Standard Time");
    }

    public static DateTimeOffset NowUtc => DateTimeOffset.UtcNow;

    public static DateTime ToCafeLocal(DateTimeOffset instant)
    {
        return TimeZoneInfo.ConvertTime(instant.UtcDateTime, IranZone);
    }

    public static DateOnly CafeToday(DateTimeOffset instant)
    {
        return DateOnly.FromDateTime(ToCafeLocal(instant));
    }
}
