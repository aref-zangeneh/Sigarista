using Sugarista.Domain.Entities;

namespace Sugarista.Application.Pickup;

/// <summary>
/// Immutable view of every café setting that drives pickup scheduling.
/// Nothing in the scheduling code reads a hard-coded slot length, capacity,
/// lead time, deadline or time zone — it all comes from here.
/// </summary>
public sealed record PickupSettingsSnapshot(
    bool Enabled,
    int SlotMinutes,
    int SlotCapacity,
    string CapacityMode,
    int MinLeadMinutes,
    int MaxDaysAhead,
    int CancelCutoffMinutes,
    string TimeZoneId,
    bool AutoRefundOnCancel,
    string RefundPolicyNote)
{
    public bool CountsItems => string.Equals(CapacityMode, "Items", StringComparison.OrdinalIgnoreCase);

    public static PickupSettingsSnapshot From(Sugarista.Domain.Entities.SiteSettings? settings) => new(
        settings?.PickupSchedulingEnabled ?? false,
        Math.Max(5, settings?.PickupSlotMinutes ?? 15),
        Math.Max(1, settings?.PickupSlotCapacity ?? 5),
        string.Equals(settings?.PickupCapacityMode, "Items", StringComparison.OrdinalIgnoreCase) ? "Items" : "Orders",
        Math.Max(0, settings?.PickupMinLeadMinutes ?? 30),
        Math.Max(0, settings?.PickupMaxDaysAhead ?? 7),
        Math.Max(0, settings?.PickupCancelCutoffMinutes ?? 60),
        string.IsNullOrWhiteSpace(settings?.TimeZoneId) ? "Asia/Tehran" : settings!.TimeZoneId,
        settings?.PickupAutoRefundOnCancel ?? true,
        settings?.PickupRefundPolicyNote ?? string.Empty);
}
