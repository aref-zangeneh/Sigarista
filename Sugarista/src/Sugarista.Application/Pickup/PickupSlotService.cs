using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.Hr;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Pickup;

public sealed record PickupSlotDto(
    string Date,
    string DateLabel,
    string WeekdayLabel,
    string Time,
    string TimeLabel,
    string EndTime,
    DateTimeOffset DeliveryAt,
    bool IsFull,
    int UsedCapacity,
    int RemainingCapacity,
    int Capacity);

public sealed record PickupDayDto(
    string Date,
    string DateLabel,
    string WeekdayLabel,
    bool IsOpen,
    string? ClosedReason,
    IReadOnlyCollection<PickupSlotDto> Slots);

public sealed record PickupScheduleDto(
    bool Enabled,
    string TimeZoneId,
    int SlotMinutes,
    int SlotCapacity,
    string CapacityMode,
    int MinLeadMinutes,
    int MaxDaysAhead,
    int CancelCutoffMinutes,
    string RefundPolicyNote,
    IReadOnlyCollection<PickupDayDto> Days);

/// <summary>
/// Loads the live café settings, opening hours and existing order usage, then
/// delegates every calculation to <see cref="PickupScheduleCalculator"/>.
/// </summary>
public sealed class PickupSlotService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IDateTime _clock;

    public PickupSlotService(IApplicationDbContext dbContext, IDateTime clock)
    {
        _dbContext = dbContext;
        _clock = clock;
    }

    public async Task<PickupSettingsSnapshot> GetSettingsAsync(CancellationToken cancellationToken)
    {
        var settings = await _dbContext.SiteSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        return PickupSettingsSnapshot.From(settings);
    }

    public async Task<IReadOnlyDictionary<int, CafeOpenHour>> GetOpenHoursAsync(CancellationToken cancellationToken)
    {
        var rows = await _dbContext.CafeOpenHours
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(row => row.Weekday)
            .ToDictionary(group => group.Key, group => group.First());
    }

    public async Task<PickupScheduleDto> GetScheduleAsync(
        int? daysAhead,
        CancellationToken cancellationToken)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        var now = _clock.UtcNow;
        var timeZone = PickupScheduleCalculator.ResolveTimeZone(settings.TimeZoneId);
        var today = PickupScheduleCalculator.GetCafeLocalDate(now, timeZone);

        if (!settings.Enabled)
        {
            return new PickupScheduleDto(
                false,
                settings.TimeZoneId,
                settings.SlotMinutes,
                settings.SlotCapacity,
                settings.CapacityMode,
                settings.MinLeadMinutes,
                settings.MaxDaysAhead,
                settings.CancelCutoffMinutes,
                settings.RefundPolicyNote,
                []);
        }

        var openHours = await GetOpenHoursAsync(cancellationToken);
        var lastDay = today.AddDays(settings.MaxDaysAhead);
        if (daysAhead is { } requestedDays)
        {
            var capped = today.AddDays(Math.Clamp(requestedDays, 0, settings.MaxDaysAhead));
            if (capped < lastDay)
            {
                lastDay = capped;
            }
        }

        var usage = await LoadUsageAsync(today, lastDay, openHours, settings, timeZone, cancellationToken);

        var days = new List<PickupDayDto>();
        for (var date = today; date <= lastDay; date = date.AddDays(1))
        {
            var weekday = (int)date.DayOfWeek;
            var openHour = openHours.GetValueOrDefault(weekday);
            var isOpen = openHour is { IsOpen: true };
            var dayUsage = usage.GetValueOrDefault(date) ?? new Dictionary<int, int>();
            var slots = PickupScheduleCalculator.BuildDaySlots(
                settings,
                openHour,
                date,
                now,
                timeZone,
                dayUsage);

            days.Add(new PickupDayDto(
                date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                PersianDateLabel(date),
                HrShamsiCalendar.WeekdayLabel(weekday),
                isOpen,
                isOpen ? null : "کافه در این روز تعطیل است.",
                slots.Select(ToDto).ToArray()));
        }

        return new PickupScheduleDto(
            true,
            settings.TimeZoneId,
            settings.SlotMinutes,
            settings.SlotCapacity,
            settings.CapacityMode,
            settings.MinLeadMinutes,
            settings.MaxDaysAhead,
            settings.CancelCutoffMinutes,
            settings.RefundPolicyNote,
            days);
    }

    public async Task<PickupValidationResult> ValidateAsync(
        DateTimeOffset requestedDeliveryAt,
        CancellationToken cancellationToken)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        var now = _clock.UtcNow;
        var timeZone = PickupScheduleCalculator.ResolveTimeZone(settings.TimeZoneId);
        var openHours = await GetOpenHoursAsync(cancellationToken);

        var localDate = PickupScheduleCalculator.GetCafeLocalDate(requestedDeliveryAt, timeZone);
        var usage = await LoadUsageAsync(localDate, localDate, openHours, settings, timeZone, cancellationToken);
        var dayUsage = usage.GetValueOrDefault(localDate) ?? new Dictionary<int, int>();

        var result = PickupScheduleCalculator.Validate(
            settings,
            openHours,
            requestedDeliveryAt,
            now,
            timeZone,
            dayUsage);

        if (result.IsValid)
        {
            return result;
        }

        var nearest = await FindNearestAsync(requestedDeliveryAt, settings, openHours, timeZone, now, cancellationToken);
        return result with { NearestAvailableSlot = nearest?.DeliveryAt };
    }

    /// <summary>
    /// Re-checks capacity while holding the slot reservation lock. Used by
    /// checkout and reschedule so a concurrent request cannot overfill a slot.
    /// </summary>
    public async Task<PickupValidationResult> ValidateCapacityOnlyAsync(
        DateTimeOffset requestedDeliveryAt,
        PickupSettingsSnapshot settings,
        IReadOnlyDictionary<int, CafeOpenHour> openHours,
        TimeZoneInfo timeZone,
        CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var localDate = PickupScheduleCalculator.GetCafeLocalDate(requestedDeliveryAt, timeZone);
        var usage = await LoadUsageAsync(localDate, localDate, openHours, settings, timeZone, cancellationToken);
        var dayUsage = usage.GetValueOrDefault(localDate) ?? new Dictionary<int, int>();

        return PickupScheduleCalculator.Validate(
            settings,
            openHours,
            requestedDeliveryAt,
            now,
            timeZone,
            dayUsage);
    }

    public async Task<PickupSlot?> FindNearestAsync(
        DateTimeOffset requestedDeliveryAt,
        PickupSettingsSnapshot settings,
        IReadOnlyDictionary<int, CafeOpenHour> openHours,
        TimeZoneInfo timeZone,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (!settings.Enabled)
        {
            return null;
        }

        var today = PickupScheduleCalculator.GetCafeLocalDate(now, timeZone);
        var lastDay = today.AddDays(Math.Min(settings.MaxDaysAhead, 14));
        var usage = await LoadUsageAsync(today, lastDay, openHours, settings, timeZone, cancellationToken);

        PickupSlot? nearest = null;
        var nearestDistance = double.MaxValue;

        for (var date = today; date <= lastDay; date = date.AddDays(1))
        {
            var openHour = openHours.GetValueOrDefault((int)date.DayOfWeek);
            var slots = PickupScheduleCalculator.BuildDaySlots(
                settings,
                openHour,
                date,
                now,
                timeZone,
                usage.GetValueOrDefault(date));

            foreach (var slot in slots.Where(slot => !slot.IsFull))
            {
                var distance = Math.Abs((slot.DeliveryAt - requestedDeliveryAt).TotalMinutes);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = slot;
                }
            }
        }

        return nearest;
    }

    private async Task<Dictionary<DateOnly, Dictionary<int, int>>> LoadUsageAsync(
        DateOnly fromDate,
        DateOnly toDate,
        IReadOnlyDictionary<int, CafeOpenHour> openHours,
        PickupSettingsSnapshot settings,
        TimeZoneInfo timeZone,
        CancellationToken cancellationToken)
    {
        var fromInstant = PickupScheduleCalculator.ToCafeInstant(fromDate, TimeOnly.MinValue, timeZone);
        var toInstant = PickupScheduleCalculator.ToCafeInstant(toDate.AddDays(1), TimeOnly.MinValue, timeZone);

        var orders = await _dbContext.Orders
            .AsNoTracking()
            .Where(order =>
                order.DeliveryAt != null &&
                order.DeliveryAt >= fromInstant &&
                order.DeliveryAt < toInstant &&
                order.Status != OrderStatus.Cancelled &&
                order.Status != OrderStatus.Failed)
            .Select(order => new
            {
                DeliveryAt = order.DeliveryAt!.Value,
                ItemCount = order.Lines.Sum(line => line.Quantity)
            })
            .ToListAsync(cancellationToken);

        var usage = new Dictionary<DateOnly, Dictionary<int, int>>();

        foreach (var order in orders)
        {
            var local = PickupScheduleCalculator.GetCafeLocalDateTime(order.DeliveryAt, timeZone);
            var date = DateOnly.FromDateTime(local);
            if (date < fromDate || date > toDate)
            {
                continue;
            }

            var openHour = openHours.GetValueOrDefault((int)date.DayOfWeek);
            if (openHour is null || !openHour.IsOpen)
            {
                continue;
            }

            var slotTime = TimeOnly.FromDateTime(local);
            var index = PickupScheduleCalculator.ResolveSlotIndex(slotTime, openHour.OpenTime, settings.SlotMinutes);
            if (index is null)
            {
                continue;
            }

            if (!usage.TryGetValue(date, out var dayUsage))
            {
                dayUsage = new Dictionary<int, int>();
                usage[date] = dayUsage;
            }

            var amount = settings.CountsItems ? Math.Max(1, order.ItemCount) : 1;
            dayUsage[index.Value] = dayUsage.GetValueOrDefault(index.Value) + amount;
        }

        return usage;
    }

    private static PickupSlotDto ToDto(PickupSlot slot) => new(
        slot.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        PersianDateLabel(slot.Date),
        HrShamsiCalendar.WeekdayLabel((int)slot.Date.DayOfWeek),
        slot.StartTime.ToString("HH:mm"),
        slot.TimeLabel,
        slot.EndTime.ToString("HH:mm"),
        slot.DeliveryAt,
        slot.IsFull,
        slot.UsedCapacity,
        slot.RemainingCapacity,
        slot.Capacity);

    private static string PersianDateLabel(DateOnly date)
    {
        var calendar = new PersianCalendar();
        var value = date.ToDateTime(TimeOnly.MinValue);
        return $"{calendar.GetDayOfMonth(value)} {HrShamsiCalendar.MonthNames[calendar.GetMonth(value) - 1]}";
    }
}
