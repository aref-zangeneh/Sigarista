using Sugarista.Application.Pickup;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.UnitTests;

/// <summary>
/// TASK-012 §9 edge cases for the pure pickup-scheduling maths: opening hours,
/// closed days, minimum lead time, capacity, DST-safe café time zone
/// conversion, and the cancel/reschedule deadline boundary.
/// </summary>
public class PickupScheduleCalculatorTests
{
    private static readonly TimeZoneInfo Tehran =
        PickupScheduleCalculator.ResolveTimeZone("Asia/Tehran");

    private static readonly DateOnly OpenDay = new(2026, 3, 10);

    private static PickupSettingsSnapshot Settings(
        bool enabled = true,
        int slotMinutes = 30,
        int capacity = 2,
        int minLeadMinutes = 0,
        int maxDaysAhead = 7,
        int cancelCutoffMinutes = 60) =>
        new(
            enabled,
            slotMinutes,
            capacity,
            "Orders",
            minLeadMinutes,
            maxDaysAhead,
            cancelCutoffMinutes,
            "Asia/Tehran",
            AutoRefundOnCancel: true,
            RefundPolicyNote: "note");

    private static CafeOpenHour Open(TimeOnly open, TimeOnly close) =>
        new((int)OpenDay.DayOfWeek, true, open, close);

    private static CafeOpenHour Closed() =>
        new((int)OpenDay.DayOfWeek, false, TimeOnly.MinValue, TimeOnly.MinValue);

    private static IReadOnlyDictionary<int, CafeOpenHour> Hours(CafeOpenHour hour) =>
        new Dictionary<int, CafeOpenHour> { [hour.Weekday] = hour };

    private static IReadOnlyDictionary<int, CafeOpenHour> AllWeekHours()
    {
        var hours = new Dictionary<int, CafeOpenHour>();
        for (var weekday = 0; weekday <= 6; weekday++)
        {
            hours[weekday] = new CafeOpenHour(weekday, true, new TimeOnly(9, 0), new TimeOnly(21, 0));
        }

        return hours;
    }

    private static DateTimeOffset At(TimeOnly localTime, DateOnly? date = null) =>
        PickupScheduleCalculator.ToCafeInstant(date ?? OpenDay, localTime, Tehran);

    private static Order NewOrder(TimeOnly? deliveryLocalTime = null)
    {
        var order = new Order("SGR-TEST-1", "مشتری تست", "09120000000", null, 100_000m, "AUTH-1");
        if (deliveryLocalTime is { } time)
        {
            order.ApplyDeliveryTime(At(time));
        }

        return order;
    }

    [Fact]
    public void BuildDaySlots_GeneratesBackToBackSlotsInsideOpeningHours()
    {
        var settings = Settings(slotMinutes: 30);
        var now = At(new TimeOnly(8, 0));

        var slots = PickupScheduleCalculator.BuildDaySlots(
            settings,
            Open(new TimeOnly(9, 0), new TimeOnly(12, 0)),
            OpenDay,
            now,
            Tehran);

        Assert.Equal(6, slots.Count);
        Assert.Equal(new TimeOnly(9, 0), slots[0].StartTime);
        Assert.Equal(new TimeOnly(9, 30), slots[0].EndTime);
        Assert.Equal(new TimeOnly(11, 30), slots[^1].StartTime);
        Assert.Equal(new TimeOnly(12, 0), slots[^1].EndTime);
        Assert.All(slots, slot => Assert.False(slot.IsFull));
    }

    [Fact]
    public void BuildDaySlots_ReturnsNothingOnClosedDaysOrWhenDisabled()
    {
        var now = At(new TimeOnly(8, 0));

        var closed = PickupScheduleCalculator.BuildDaySlots(
            Settings(),
            Closed(),
            OpenDay,
            now,
            Tehran);
        Assert.Empty(closed);

        var disabled = PickupScheduleCalculator.BuildDaySlots(
            Settings(enabled: false),
            Open(new TimeOnly(9, 0), new TimeOnly(12, 0)),
            OpenDay,
            now,
            Tehran);
        Assert.Empty(disabled);
    }

    [Fact]
    public void BuildDaySlots_OmitsSlotsInsideTheMinimumLeadTime()
    {
        var settings = Settings(slotMinutes: 30, minLeadMinutes: 30);
        var now = At(new TimeOnly(10, 10));

        var slots = PickupScheduleCalculator.BuildDaySlots(
            settings,
            Open(new TimeOnly(9, 0), new TimeOnly(12, 0)),
            OpenDay,
            now,
            Tehran);

        // 09:00, 09:30 and 10:00 are before 10:40; the first valid slot is 11:00.
        Assert.Equal(2, slots.Count);
        Assert.Equal(new TimeOnly(11, 0), slots[0].StartTime);
    }

    [Fact]
    public void BuildDaySlots_KeepsFullSlotsAndMarksThemAsFull()
    {
        var usage = new Dictionary<int, int> { [0] = 2 };
        var now = At(new TimeOnly(8, 0));

        var slots = PickupScheduleCalculator.BuildDaySlots(
            Settings(capacity: 2),
            Open(new TimeOnly(9, 0), new TimeOnly(11, 0)),
            OpenDay,
            now,
            Tehran,
            usage);

        Assert.Equal(4, slots.Count);
        Assert.True(slots[0].IsFull);
        Assert.Equal(0, slots[0].RemainingCapacity);
        Assert.False(slots[1].IsFull);
        Assert.Equal(2, slots[1].RemainingCapacity);
    }

    [Fact]
    public void Validate_AcceptsASlotOnTheBoundaryInsideOpeningHours()
    {
        var settings = Settings();
        var result = PickupScheduleCalculator.Validate(
            settings,
            Hours(Open(new TimeOnly(9, 0), new TimeOnly(21, 0))),
            At(new TimeOnly(9, 0)),
            At(new TimeOnly(8, 0)),
            Tehran,
            new Dictionary<int, int>());

        Assert.True(result.IsValid);
        Assert.NotNull(result.Slot);
        Assert.Equal(new TimeOnly(9, 0), result.Slot!.StartTime);
    }

    [Fact]
    public void Validate_RejectsPastDatesClosedDaysAndDatesBeyondMaxDays()
    {
        var settings = Settings(maxDaysAhead: 1);
        var hours = AllWeekHours();
        var now = At(new TimeOnly(8, 0));

        var past = PickupScheduleCalculator.Validate(
            settings,
            hours,
            At(new TimeOnly(10, 0), OpenDay.AddDays(-1)),
            now,
            Tehran,
            new Dictionary<int, int>());
        Assert.False(past.IsValid);
        Assert.Equal("past_date", past.Code);

        var beyond = PickupScheduleCalculator.Validate(
            settings,
            hours,
            At(new TimeOnly(10, 0), OpenDay.AddDays(2)),
            now,
            Tehran,
            new Dictionary<int, int>());
        Assert.False(beyond.IsValid);
        Assert.Equal("beyond_max_days", beyond.Code);

        // Friday is a closed day in this fixture.
        var friday = new DateOnly(2026, 3, 13);
        Assert.Equal(DayOfWeek.Friday, friday.DayOfWeek);
        var closed = PickupScheduleCalculator.Validate(
            Settings(maxDaysAhead: 7),
            new Dictionary<int, CafeOpenHour>
            {
                [(int)DayOfWeek.Friday] = new((int)DayOfWeek.Friday, false, TimeOnly.MinValue, TimeOnly.MinValue)
            },
            PickupScheduleCalculator.ToCafeInstant(friday, new TimeOnly(10, 0), Tehran),
            now,
            Tehran,
            new Dictionary<int, int>());
        Assert.False(closed.IsValid);
        Assert.Equal("closed_day", closed.Code);
    }

    [Fact]
    public void Validate_RejectsOffBoundaryAndOutsideHoursTimes()
    {
        var settings = Settings();
        var hours = Hours(Open(new TimeOnly(9, 0), new TimeOnly(12, 10)));
        var now = At(new TimeOnly(8, 0));

        var offBoundary = PickupScheduleCalculator.Validate(
            settings,
            hours,
            At(new TimeOnly(9, 10)),
            now,
            Tehran,
            new Dictionary<int, int>());
        Assert.False(offBoundary.IsValid);
        Assert.Equal("invalid_slot", offBoundary.Code);

        // 12:00 is a valid slot boundary, but a 30-minute slot would end after
        // the 12:10 closing time.
        var outside = PickupScheduleCalculator.Validate(
            settings,
            hours,
            At(new TimeOnly(12, 0)),
            now,
            Tehran,
            new Dictionary<int, int>());
        Assert.False(outside.IsValid);
        Assert.Equal("outside_hours", outside.Code);
    }

    [Fact]
    public void Validate_RejectsTooSoonAndFullSlots()
    {
        var settings = Settings(capacity: 2, minLeadMinutes: 30);
        var hours = Hours(Open(new TimeOnly(9, 0), new TimeOnly(21, 0)));

        var tooSoon = PickupScheduleCalculator.Validate(
            settings,
            hours,
            At(new TimeOnly(9, 30)),
            At(new TimeOnly(9, 5)),
            Tehran,
            new Dictionary<int, int>());
        Assert.False(tooSoon.IsValid);
        Assert.Equal("too_soon", tooSoon.Code);

        var full = PickupScheduleCalculator.Validate(
            settings,
            hours,
            At(new TimeOnly(10, 0)),
            At(new TimeOnly(8, 0)),
            Tehran,
            new Dictionary<int, int> { [2] = 2 });
        Assert.False(full.IsValid);
        Assert.Equal("slot_full", full.Code);
    }

    [Fact]
    public void Validate_RejectsEverythingWhenSchedulingIsDisabled()
    {
        var result = PickupScheduleCalculator.Validate(
            Settings(enabled: false),
            Hours(Open(new TimeOnly(9, 0), new TimeOnly(21, 0))),
            At(new TimeOnly(10, 0)),
            At(new TimeOnly(8, 0)),
            Tehran,
            new Dictionary<int, int>());

        Assert.False(result.IsValid);
        Assert.Equal("scheduling_disabled", result.Code);
    }

    [Fact]
    public void ToCafeInstant_RoundTripsThroughTheCafeTimeZone()
    {
        var instant = At(new TimeOnly(9, 30));
        var local = PickupScheduleCalculator.GetCafeLocalDateTime(instant, Tehran);

        Assert.Equal(OpenDay, DateOnly.FromDateTime(local));
        Assert.Equal(new TimeOnly(9, 30), TimeOnly.FromDateTime(local));
        // Asia/Tehran is UTC+03:30, so 09:30 local is 06:00 UTC.
        Assert.Equal(new TimeSpan(6, 0, 0), instant.UtcDateTime.TimeOfDay);
    }

    [Fact]
    public void IsWithinChangeWindow_AllowsExactlyUntilTheCutoffBoundary()
    {
        var settings = Settings(cancelCutoffMinutes: 60);
        var order = NewOrder(new TimeOnly(12, 0));
        var deliveryAt = order.DeliveryAt!.Value;

        Assert.True(PickupScheduleCalculator.IsWithinChangeWindow(
            order, settings, deliveryAt.AddMinutes(-60)));
        Assert.False(PickupScheduleCalculator.IsWithinChangeWindow(
            order, settings, deliveryAt.AddMinutes(-60).AddSeconds(1)));
        Assert.Null(PickupScheduleCalculator.BuildChangeBlockedReason(
            order, settings, deliveryAt.AddMinutes(-60)));
        Assert.Equal(
            "مهلت لغو گذشته است.",
            PickupScheduleCalculator.BuildChangeBlockedReason(
                order, settings, deliveryAt.AddMinutes(-59)));
    }

    [Fact]
    public void BuildChangeBlockedReason_ExplainsCancelledOrders()
    {
        var settings = Settings();
        var order = NewOrder(new TimeOnly(12, 0));
        order.AdminCancel();

        Assert.Equal(
            "این سفارش قبلاً لغو شده است.",
            PickupScheduleCalculator.BuildChangeBlockedReason(order, settings, At(new TimeOnly(9, 0))));
    }

    [Fact]
    public void ResolveSlotIndex_OnlyMatchesExactBoundaries()
    {
        var open = new TimeOnly(9, 0);

        Assert.Equal(0, PickupScheduleCalculator.ResolveSlotIndex(new TimeOnly(9, 0), open, 30));
        Assert.Equal(3, PickupScheduleCalculator.ResolveSlotIndex(new TimeOnly(10, 30), open, 30));
        Assert.Null(PickupScheduleCalculator.ResolveSlotIndex(new TimeOnly(10, 15), open, 30));
        Assert.Null(PickupScheduleCalculator.ResolveSlotIndex(new TimeOnly(8, 45), open, 30));
    }
}
