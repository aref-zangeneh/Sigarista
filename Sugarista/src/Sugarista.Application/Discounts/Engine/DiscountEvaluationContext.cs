using Sugarista.Domain.Entities;

namespace Sugarista.Application.Discounts.Engine;

/// <summary>One cart line as the discount engine sees it.</summary>
public sealed record DiscountLineInput(
    Guid MenuItemId,
    Guid? CategoryId,
    string PersianName,
    string? SizeKey,
    int Quantity,
    decimal UnitPrice,
    string? PayerKey = null)
{
    public decimal LineTotal => UnitPrice * Quantity;
}

/// <summary>
/// Everything a discount may depend on. Callers (online checkout, POS, admin
/// preview) build this from their own data, so the engine never queries carts
/// or customer history itself.
/// </summary>
public sealed record DiscountEvaluationContext(
    IReadOnlyCollection<DiscountLineInput> Lines,
    decimal DeliveryFee,
    string? Code,
    Guid? CustomerId,
    IReadOnlyCollection<DiscountCustomerGroup>? CustomerGroups,
    DateTimeOffset Now,
    string Channel = "online",
    bool IsOnlinePayment = false,
    string TimeZoneId = "Asia/Tehran",
    DateTimeOffset? FulfillmentTime = null)
{
    /// <summary>The moment discounts are evaluated against (delivery time if known, else now).</summary>
    public DateTimeOffset EffectiveTime => FulfillmentTime ?? Now;

    /// <summary>Groups the customer belongs to; <see cref="DiscountCustomerGroup.All"/> when unknown.</summary>
    public IReadOnlyCollection<DiscountCustomerGroup> EffectiveGroups
        => CustomerGroups is { Count: > 0 } ? CustomerGroups : [DiscountCustomerGroup.All];
}

/// <summary>Where a discount's money landed, so callers can reduce lines and print receipts.</summary>
public sealed record DiscountLineShare(
    Guid MenuItemId,
    string? SizeKey,
    string? PayerKey,
    decimal Amount);

public sealed record DiscountApplication(
    Guid DiscountId,
    string Name,
    string? Code,
    DiscountType Type,
    decimal Amount,
    IReadOnlyCollection<DiscountLineShare> LineShares,
    string Message);

public enum DiscountRejectionReason
{
    NotFound = 0,
    Inactive = 1,
    NotStarted = 2,
    Expired = 3,
    OutsideSchedule = 4,
    MinCartNotMet = 5,
    TotalUsesExceeded = 6,
    CustomerUsesExceeded = 7,
    CustomerGroupMismatch = 8,
    NoEligibleItems = 9,
    NotStackable = 10,
    NotAutomatic = 11
}

public sealed record DiscountRejection(
    Guid? DiscountId,
    string? Code,
    DiscountRejectionReason Reason,
    string Message);

public sealed record DiscountEvaluationResult(
    decimal ItemsSubtotal,
    decimal DiscountTotal,
    IReadOnlyCollection<DiscountApplication> Applications,
    IReadOnlyCollection<DiscountRejection> Rejections)
{
    public decimal TotalAfterDiscount => Math.Max(0, ItemsSubtotal - DiscountTotal);

    /// <summary>The single rejection to show the customer, when a code was typed.</summary>
    public DiscountRejection? CodeRejection => Rejections.FirstOrDefault();
}
