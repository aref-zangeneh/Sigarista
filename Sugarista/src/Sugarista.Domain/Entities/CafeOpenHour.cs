using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

/// <summary>
/// Structured opening-hours row of the café for one weekday.
/// Weekday follows .NET DayOfWeek (Sunday = 0 ... Saturday = 6) so the
/// Persian weekday mapping (شنبه .. جمعه) stays a pure presentation concern.
/// </summary>
public class CafeOpenHour : BaseAuditableEntity
{
    private CafeOpenHour()
    {
    }

    public CafeOpenHour(int weekday, bool isOpen, TimeOnly openTime, TimeOnly closeTime)
    {
        if (weekday is < 0 or > 6)
        {
            throw new DomainException("روز هفته نامعتبر است.");
        }

        Validate(isOpen, openTime, closeTime);

        Weekday = weekday;
        IsOpen = isOpen;
        OpenTime = openTime;
        CloseTime = closeTime;
    }

    public int Weekday { get; private set; }

    public bool IsOpen { get; private set; }

    public TimeOnly OpenTime { get; private set; }

    public TimeOnly CloseTime { get; private set; }

    /// <summary>Scheduled minutes of service for this weekday.</summary>
    public decimal ScheduledMinutes => IsOpen
        ? (decimal)((CloseTime.ToTimeSpan() - OpenTime.ToTimeSpan()).TotalMinutes)
        : 0m;

    public void Update(bool isOpen, TimeOnly openTime, TimeOnly closeTime)
    {
        Validate(isOpen, openTime, closeTime);
        IsOpen = isOpen;
        OpenTime = openTime;
        CloseTime = closeTime;
    }

    private static void Validate(bool isOpen, TimeOnly openTime, TimeOnly closeTime)
    {
        if (!isOpen)
        {
            return;
        }

        if (closeTime <= openTime)
        {
            throw new DomainException("ساعت بسته شدن باید بعد از ساعت باز شدن باشد.");
        }
    }
}
