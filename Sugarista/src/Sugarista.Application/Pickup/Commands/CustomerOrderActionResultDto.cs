namespace Sugarista.Application.Pickup.Commands;

/// <summary>Result of a customer-initiated cancel or reschedule.</summary>
public sealed record CustomerOrderActionResultDto(
    string TrackingCode,
    string Status,
    string FulfillmentStatus,
    DateTimeOffset? DeliveryAt,
    string Message,
    decimal RefundedAmount = 0,
    bool RefundHandledByCafe = false);
