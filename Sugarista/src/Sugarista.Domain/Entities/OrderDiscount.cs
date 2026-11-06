using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

/// <summary>
/// The kind of a discount line recorded on an order. Unlike
/// <see cref="DiscountRedemption"/> (which only covers engine discounts),
/// this covers every benefit applied to an order so the performance report
/// and the per-customer daily cap see the full picture.
/// </summary>
public enum OrderDiscountKind
{
    /// <summary>A code/automatic discount evaluated by the discount engine.</summary>
    Code = 0,

    /// <summary>Standing membership-tier percent.</summary>
    Tier = 1,

    /// <summary>Standing customer-group percent.</summary>
    Group = 2,

    /// <summary>Automatic birthday benefit.</summary>
    Birthday = 3,

    /// <summary>Personal-cup fixed discount.</summary>
    Cup = 4,

    /// <summary>Monthly subscription unit redemption.</summary>
    Subscription = 5,

    /// <summary>Loyalty points converted to a discount.</summary>
    LoyaltyPoints = 6,

    /// <summary>Stamp-card reward.</summary>
    StampCard = 7,

    /// <summary>Referral reward.</summary>
    Referral = 8,
}

/// <summary>
/// One discount amount applied to an order, with an optional category scope.
/// Recorded for every benefit (code, tier, group, birthday, cup, subscription,
/// points, stamp card) so reports can break totals down by discount and
/// category, and so the daily per-customer cap can be enforced.
/// </summary>
public class OrderDiscount : BaseEntity
{
    private OrderDiscount()
    {
    }

    public OrderDiscount(
        Guid orderId,
        Guid? customerId,
        OrderDiscountKind kind,
        string name,
        decimal amount,
        Guid? discountId = null,
        string? code = null,
        Guid? categoryId = null,
        DateTimeOffset? redeemedAt = null)
    {
        if (orderId == Guid.Empty)
        {
            throw new DomainException("An order discount needs an order.");
        }

        if (amount < 0)
        {
            throw new DomainException("A discount amount cannot be negative.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("A discount line needs a name.");
        }

        OrderId = orderId;
        CustomerId = customerId;
        Kind = kind;
        Name = name.Trim();
        Amount = amount;
        DiscountId = discountId;
        Code = Discount.NormalizeCode(code);
        CategoryId = categoryId;
        RedeemedAt = redeemedAt ?? DateTimeOffset.UtcNow;
    }

    public Guid OrderId { get; private set; }

    public Order Order { get; private set; } = null!;

    public Guid? CustomerId { get; private set; }

    public Customer? Customer { get; private set; }

    public OrderDiscountKind Kind { get; private set; }

    /// <summary>Human readable label shown in reports (e.g. group/package name).</summary>
    public string Name { get; private set; } = string.Empty;

    public decimal Amount { get; private set; }

    /// <summary>The engine discount row, when this line came from a code/automatic discount.</summary>
    public Guid? DiscountId { get; private set; }

    public Discount? Discount { get; private set; }

    /// <summary>The typed code, when one was used.</summary>
    public string? Code { get; private set; }

    /// <summary>Category scope of the discount line (null = whole order).</summary>
    public Guid? CategoryId { get; private set; }

    public DateTimeOffset RedeemedAt { get; private set; }
}
