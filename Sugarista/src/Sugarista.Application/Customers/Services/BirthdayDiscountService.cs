using Sugarista.Domain.Entities;

namespace Sugarista.Application.Customers.Services;

/// <summary>
/// Birthday discount scheduling. The customer's birth date is compared to
/// today ignoring the year (Feb 29 anchors onto Feb 28 in non-leap years),
/// so the benefit recurs every year inside the configured window: only the
/// birthday itself (window 0), the birthday week (7) or a broader window.
/// The comparison is circular across the year boundary too — a Dec 31
/// birthday still matches on Jan 2 with a window of a few days.
/// </summary>
public static class BirthdayDiscountService
{
    /// <summary>true when the customer's birthday falls within the window around today.</summary>
    public static bool IsBirthdayActive(DateOnly dateOfBirth, int windowDays, DateOnly? today = null)
    {
        var reference = today ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var distance = Math.Abs(NearestBirthdayDistance(dateOfBirth, reference));
        return distance <= Math.Max(0, windowDays);
    }

    /// <summary>
    /// Coupon bounds for the currently active birthday window centered on the
    /// customer's birthday (window 0 = the birthday day itself). Returns null
    /// bounds when today is outside any activation window.
    /// </summary>
    public static (DateTimeOffset? StartsAt, DateTimeOffset? EndsAt) GetActiveWindow(
        DateOnly dateOfBirth, int windowDays, DateTimeOffset now)
    {
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        if (!IsBirthdayActive(dateOfBirth, windowDays, today))
        {
            return (null, null);
        }

        // The window is the biggest contiguous match centered on the birthday:
        // walk outward from the birthday day by day while still inside the
        // window as seen from each side. Simplest honest bound: [today -
        // window, today + window] ∩ [birthday - window, birthday + window].
        var span = Math.Max(0, windowDays);
        var birthday = today.AddDays(NearestBirthdayDistance(dateOfBirth, today));
        var boundStart = birthday.AddDays(-span);
        var boundEnd = birthday.AddDays(span).AddDays(1);

        var start = today.AddDays(-span);
        var end = today.AddDays(span).AddDays(1);
        return (
            new DateTimeOffset(boundStart > start ? boundStart : start, TimeOnly.MinValue, TimeSpan.Zero),
            new DateTimeOffset(boundEnd < end ? boundEnd : end, TimeOnly.MinValue, TimeSpan.Zero));
    }

    private static int NearestBirthdayDistance(DateOnly dateOfBirth, DateOnly reference)
    {
        int Best(int year)
        {
            try
            {
                return new DateOnly(year, dateOfBirth.Month, dateOfBirth.Day).DayNumber - reference.DayNumber;
            }
            catch (ArgumentOutOfRangeException)
            {
                // Feb 29 in a non-leap year → anchor on Feb 28.
                return new DateOnly(year, dateOfBirth.Month, 28).DayNumber - reference.DayNumber;
            }
        }

        var candidates = new[] { Best(reference.Year - 1), Best(reference.Year), Best(reference.Year + 1) };
        return candidates.OrderBy(Math.Abs).First();
    }
}
