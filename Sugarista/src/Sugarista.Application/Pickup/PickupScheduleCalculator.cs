using Sugarista.Domain.Entities;

namespace Sugarista.Application.Pickup;

/// <summary>One selectable pickup slot, already expressed in café local time.</summary>
public sealed record PickupSlot(
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    DateTimeOffset DeliveryAt,
    bool IsFull,
    int UsedCapacity,
    int Capacity)
{
    public int RemainingCapacity => Math.Max(0, Capacity - UsedCapacity);

    public string TimeLabel => StartTime.ToString("HH:mm");
}

/// <summary>Outcome of validating one requested pickup time against the café rules.</summary>
public sealed record PickupValidationResult(
    bool IsValid,
    string? Code,
    string? Message,
    DateTimeOffset? NearestAvailableSlot,
    PickupSlot? Slot)
{
    public static PickupValidationResult Valid(PickupSlot slot) => new(true, null, null, null, slot);

    public static PickupValidationResult Invalid(string code, string message, DateTimeOffset? nearest = null) =>
        new(false, code, message, nearest, null);
}

/// <summary>
/// Pure pickup-scheduling maths. Kept free of EF Core so every edge case
/// (closing time, closed days, lead time, capacity, DST-safe conversion) can be
/// unit-tested without a database.
/// </summary>
public static class PickupScheduleCalculator
{
    public static TimeZoneInfo ResolveTimeZone(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return TimeZoneInfo.Utc;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }

    public static DateTime GetCafeLocalDateTime(DateTimeOffset instant, TimeZoneInfo timeZone) =>
        TimeZoneInfo.ConvertTime(instant, timeZone).DateTime;

    public static DateOnly GetCafeLocalDate(DateTimeOffset instant, TimeZoneInfo timeZone) =>
        DateOnly.FromDateTime(GetCafeLocalDateTime(instant, timeZone));

    /// <summary>
    /// Converts a café-local date/time into an absolute instant. DST-safe:
    /// ambiguous local times use the standard offset and invalid times are
    /// pushed forward by the time zone rules.
    /// </summary>
    public static DateTimeOffset ToCafeInstant(DateOnly date, TimeOnly time, TimeZoneInfo timeZone)
    {
        var local = DateTime.SpecifyKind(date.ToDateTime(time), DateTimeKind.Unspecified);
        var utc = TimeZoneInfo.ConvertTimeToUtc(local, timeZone);

        // PostgreSQL only accepts offset 0 for timestamptz parameters, so all
        // slot instants are stored/compared in UTC and converted to café local
        // time only for display and weekday/hour checks.
        return new DateTimeOffset(utc, TimeSpan.Zero);
    }

    /// <summary>
    /// Builds every slot of one weekday. Full slots are kept in the result (the
    /// UI disables them with «تکمیل»); only slots outside opening hours, in the
    /// past, or inside the minimum lead time are omitted.
    /// </summary>
    public static IReadOnlyList<PickupSlot> BuildDaySlots(
        PickupSettingsSnapshot settings,
        CafeOpenHour? openHour,
        DateOnly date,
        DateTimeOffset nowUtc,
        TimeZoneInfo timeZone,
        IReadOnlyDictionary<int, int>? usageBySlotIndex = null)
    {
        if (!settings.Enabled || openHour is null || !openHour.IsOpen)
        {
            return [];
        }

        var slotMinutes = Math.Max(5, settings.SlotMinutes);
        var capacity = Math.Max(1, settings.SlotCapacity);
        var earliest = nowUtc.UtcDateTime.AddMinutes(Math.Max(0, settings.MinLeadMinutes));

        var slots = new List<PickupSlot>();
        var cursor = openHour.OpenTime;
        var index = 0;

        while (cursor.AddMinutes(slotMinutes) <= openHour.CloseTime)
        {
            var deliveryAt = ToCafeInstant(date, cursor, timeZone);
            if (deliveryAt.UtcDateTime >= earliest)
            {
                var used = usageBySlotIndex?.GetValueOrDefault(index) ?? 0;
                slots.Add(new PickupSlot(
                    date,
                    cursor,
                    cursor.AddMinutes(slotMinutes),
                    deliveryAt,
                    used >= capacity,
                    used,
                    capacity));
            }

            cursor = cursor.AddMinutes(slotMinutes);
            index++;
        }

        return slots;
    }

    /// <summary>Slot index within the day, or null when the time is not on a slot boundary.</summary>
    public static int? ResolveSlotIndex(TimeOnly slotTime, TimeOnly openTime, int slotMinutes)
    {
        if (slotMinutes <= 0)
        {
            return null;
        }

        var minutes = (slotTime.ToTimeSpan() - openTime.ToTimeSpan()).TotalMinutes;
        if (minutes < 0)
        {
            return null;
        }

        var index = (int)(minutes / slotMinutes);
        var remainder = minutes - (index * slotMinutes);
        return Math.Abs(remainder) < 0.0001 ? index : null;
    }

    /// <summary>
    /// Validates a requested delivery instant against the live settings and
    /// the day's usage. Returns a Persian, actionable reason when invalid.
    /// </summary>
    public static PickupValidationResult Validate(
        PickupSettingsSnapshot settings,
        IReadOnlyDictionary<int, CafeOpenHour> openHoursByWeekday,
        DateTimeOffset requestedDeliveryAt,
        DateTimeOffset nowUtc,
        TimeZoneInfo timeZone,
        IReadOnlyDictionary<int, int> usageBySlotIndex)
    {
        if (!settings.Enabled)
        {
            return PickupValidationResult.Invalid(
                "scheduling_disabled",
                "زمان‌بندی تحویل حضوری در حال حاضر فعال نیست.");
        }

        var local = GetCafeLocalDateTime(requestedDeliveryAt, timeZone);
        var localDate = DateOnly.FromDateTime(local);
        var localTime = TimeOnly.FromDateTime(local);
        var today = GetCafeLocalDate(nowUtc, timeZone);

        if (localDate < today)
        {
            return PickupValidationResult.Invalid(
                "past_date",
                "این زمان گذشته است؛ لطفاً یک بازهٔ آینده انتخاب کنید.");
        }

        if (localDate > today.AddDays(settings.MaxDaysAhead))
        {
            return PickupValidationResult.Invalid(
                "beyond_max_days",
                $"حداکثر تا {settings.MaxDaysAhead} روز آینده می‌توانید سفارش ثبت کنید.");
        }

        var weekday = (int)local.DayOfWeek;
        if (!openHoursByWeekday.TryGetValue(weekday, out var openHour) || !openHour.IsOpen)
        {
            return PickupValidationResult.Invalid(
                "closed_day",
                "کافه در این روز تعطیل است؛ لطفاً روز دیگری را انتخاب کنید.");
        }

        var index = ResolveSlotIndex(localTime, openHour.OpenTime, settings.SlotMinutes);
        if (index is null)
        {
            return PickupValidationResult.Invalid(
                "invalid_slot",
                $"این زمان با بازه‌های {settings.SlotMinutes} دقیقه‌ای هم‌خوان نیست؛ لطفاً یکی از بازه‌های پیشنهادی را انتخاب کنید.");
        }

        var slotEnd = localTime.AddMinutes(settings.SlotMinutes);
        if (localTime < openHour.OpenTime || slotEnd > openHour.CloseTime)
        {
            return PickupValidationResult.Invalid(
                "outside_hours",
                "این زمان خارج از ساعت کاری کافه است؛ لطفاً بازهٔ دیگری انتخاب کنید.");
        }

        var earliest = nowUtc.UtcDateTime.AddMinutes(Math.Max(0, settings.MinLeadMinutes));
        if (requestedDeliveryAt.UtcDateTime < earliest)
        {
            return PickupValidationResult.Invalid(
                "too_soon",
                $"حداقل {settings.MinLeadMinutes} دقیقه پیش از تحویل باید سفارش ثبت شود؛ لطفاً بازهٔ دیرتری انتخاب کنید.");
        }

        var capacity = Math.Max(1, settings.SlotCapacity);
        var used = usageBySlotIndex.GetValueOrDefault(index.Value);
        var slot = new PickupSlot(
            localDate,
            localTime,
            slotEnd,
            requestedDeliveryAt.ToUniversalTime(),
            used >= capacity,
            used,
            capacity);

        if (slot.IsFull)
        {
            return PickupValidationResult.Invalid(
                "slot_full",
                "این بازه تکمیل شده است؛ لطفاً بازهٔ نزدیک دیگری انتخاب کنید.");
        }

        return PickupValidationResult.Valid(slot);
    }

    /// <summary>
    /// True when the customer is still inside the café's cancel/reschedule
    /// window. A null DeliveryAt (as-soon-as-possible order) may be changed
    /// only while the kitchen has not started preparing it.
    /// </summary>
    public static bool IsWithinChangeWindow(
        Order order,
        PickupSettingsSnapshot settings,
        DateTimeOffset nowUtc)
    {
        if (order.Status is OrderStatus.Cancelled or OrderStatus.Failed)
        {
            return false;
        }

        if (order.FulfillmentStatus == FulfillmentStatus.Completed)
        {
            return false;
        }

        if (order.DeliveryAt is not { } deliveryAt)
        {
            return order.FulfillmentStatus == FulfillmentStatus.Registered;
        }

        return nowUtc <= deliveryAt.AddMinutes(-Math.Max(0, settings.CancelCutoffMinutes));
    }

    /// <summary>
    /// True when the customer may still reschedule: the change window is open
    /// and the café's reschedule cap (if any) has not been reached.
    /// </summary>
    public static bool CanReschedule(
        Order order,
        PickupSettingsSnapshot settings,
        DateTimeOffset nowUtc)
    {
        if (!IsWithinChangeWindow(order, settings, nowUtc))
        {
            return false;
        }

        return !settings.HasRescheduleLimit || order.RescheduleCount < settings.MaxReschedules;
    }

    /// <summary>
    /// Persian reason why rescheduling is unavailable, or null when allowed.
    /// Cancelling is intentionally unaffected by the reschedule cap.
    /// </summary>
    public static string? BuildRescheduleBlockedReason(
        Order order,
        PickupSettingsSnapshot settings,
        DateTimeOffset nowUtc)
    {
        var changeBlockedReason = BuildChangeBlockedReason(order, settings, nowUtc);
        if (changeBlockedReason is not null)
        {
            return changeBlockedReason;
        }

        if (settings.HasRescheduleLimit && order.RescheduleCount >= settings.MaxReschedules)
        {
            return settings.MaxReschedules == 1
                ? "این سفارش یک بار تغییر زمان داده است؛ امکان تغییر دوباره وجود ندارد."
                : $"این سفارش {settings.MaxReschedules} بار تغییر زمان داده است؛ سقف تغییر زمان به پایان رسیده است.";
        }

        return null;
    }

    public static string? BuildChangeBlockedReason(
        Order order,
        PickupSettingsSnapshot settings,
        DateTimeOffset nowUtc)
    {
        if (order.Status is OrderStatus.Cancelled)
        {
            return "این سفارش قبلاً لغو شده است.";
        }

        if (order.Status is OrderStatus.Failed)
        {
            return "این سفارش ناموفق بوده است.";
        }

        if (order.FulfillmentStatus == FulfillmentStatus.Completed)
        {
            return "این سفارش تحویل داده شده است.";
        }

        if (order.DeliveryAt is null)
        {
            return order.FulfillmentStatus == FulfillmentStatus.Registered
                ? null
                : "آماده‌سازی سفارش شروع شده و امکان لغو یا تغییر نیست.";
        }

        return IsWithinChangeWindow(order, settings, nowUtc)
            ? null
            : "مهلت لغو گذشته است.";
    }
}
