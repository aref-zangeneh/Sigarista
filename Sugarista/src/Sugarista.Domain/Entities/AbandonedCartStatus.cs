namespace Sugarista.Domain.Entities;

/// <summary>
/// Lifecycle of a server-side cart snapshot. A cart starts <see cref="Active"/>,
/// becomes <see cref="Reminded"/> after the comeback code is delivered, and ends
/// as <see cref="Converted"/> (paid order) or <see cref="Expired"/> (stale).
/// </summary>
public enum AbandonedCartStatus
{
    /// <summary>Waiting for the configured inactivity window to elapse.</summary>
    Active = 0,

    /// <summary>The reminder (SMS/email) with a single-use code has been sent.</summary>
    Reminded = 1,

    /// <summary>A paid order arrived for this cart, so no reminder is needed.</summary>
    Converted = 2,

    /// <summary>The cart is too old to be worth reminding about.</summary>
    Expired = 3
}
