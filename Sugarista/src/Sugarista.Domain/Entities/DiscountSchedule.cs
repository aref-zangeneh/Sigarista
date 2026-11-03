using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

/// <summary>
/// A recurring time window a discount is limited to (happy hour, specific
/// weekdays). No rows on a discount means "any time". Windows may wrap
/// midnight: 22:00-02:00 is expressed as start 22:00, end 02:00.
/// </summary>
public class DiscountSchedule : BaseEntity
{
    private DiscountSchedule()
    {
    }

    public DiscountSchedule(Guid discountId, DayOfWeek? dayOfWeek, TimeOnly? startTime, TimeOnly? endTime)
    {
        if (startTime.HasValue != endTime.HasValue)
        {
            throw new DomainException("A discount time window needs both a start and an end time.");
        }

        if (startTime.HasValue && startTime == endTime)
        {
            throw new DomainException("A discount time window cannot start and end at the same time.");
        }

        DiscountId = discountId;
        DayOfWeek = dayOfWeek;
        StartTime = startTime;
        EndTime = endTime;
    }

    public Guid DiscountId { get; private set; }

    public Discount Discount { get; private set; } = null!;

    /// <summary>Null means every day of the week.</summary>
    public DayOfWeek? DayOfWeek { get; private set; }

    /// <summary>Null means the whole day.</summary>
    public TimeOnly? StartTime { get; private set; }

    public TimeOnly? EndTime { get; private set; }

    public bool Matches(DateTimeOffset moment)
    {
        if (DayOfWeek.HasValue && moment.DayOfWeek != DayOfWeek.Value)
        {
            return false;
        }

        if (!StartTime.HasValue || !EndTime.HasValue)
        {
            return true;
        }

        var time = TimeOnly.FromDateTime(moment.DateTime);
        return StartTime.Value <= EndTime.Value
            ? time >= StartTime.Value && time <= EndTime.Value
            : time >= StartTime.Value || time <= EndTime.Value; // wraps midnight
    }
}
