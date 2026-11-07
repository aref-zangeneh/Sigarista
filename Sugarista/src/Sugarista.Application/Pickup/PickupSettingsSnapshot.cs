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
    string RefundMode,
    int MaxReschedules,
    string RefundPolicyNote)
{
    public bool CountsItems => string.Equals(CapacityMode, "Items", StringComparison.OrdinalIgnoreCase);

    /// <summary>True when cancelling a paid order refunds to the customer wallet automatically.</summary>
    public bool RefundsToWallet => !string.Equals(RefundMode, "CardToCard", StringComparison.OrdinalIgnoreCase);

    /// <summary>0 means the customer may reschedule an unlimited number of times.</summary>
    public bool HasRescheduleLimit => MaxReschedules > 0;

    public static PickupSettingsSnapshot From(Sugarista.Domain.Entities.SiteSettings? settings) => new(
        settings?.PickupSchedulingEnabled ?? false,
        Math.Max(5, settings?.PickupSlotMinutes ?? 15),
        Math.Max(1, settings?.PickupSlotCapacity ?? 5),
        string.Equals(settings?.PickupCapacityMode, "Items", StringComparison.OrdinalIgnoreCase) ? "Items" : "Orders",
        Math.Max(0, settings?.PickupMinLeadMinutes ?? 30),
        Math.Max(0, settings?.PickupMaxDaysAhead ?? 7),
        Math.Max(0, settings?.PickupCancelCutoffMinutes ?? 60),
        string.IsNullOrWhiteSpace(settings?.TimeZoneId) ? "Asia/Tehran" : settings!.TimeZoneId,
        string.Equals(settings?.PickupRefundMode, "CardToCard", StringComparison.OrdinalIgnoreCase) ? "CardToCard" : "Wallet",
        Math.Max(0, settings?.PickupMaxReschedules ?? 2),
        settings?.PickupRefundPolicyNote ?? string.Empty);
}
