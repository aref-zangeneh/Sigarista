using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

/// <summary>
/// One use of a discount. Rows are written when an order is paid, and they are
/// what the engine counts against the discount's total / per-customer limits.
/// </summary>
public class DiscountRedemption : BaseEntity
{
    private DiscountRedemption()
    {
    }

    public DiscountRedemption(
        Guid discountId,
        Guid orderId,
        Guid? customerId,
        string? code,
        decimal amount,
        DateTimeOffset redeemedAt)
    {
        if (amount < 0)
        {
            throw new DomainException("A redeemed discount amount cannot be negative.");
        }

        DiscountId = discountId;
        OrderId = orderId;
        CustomerId = customerId;
        Code = Discount.NormalizeCode(code);
        Amount = amount;
        RedeemedAt = redeemedAt;
    }

    public Guid DiscountId { get; private set; }

    public Discount Discount { get; private set; } = null!;

    public Guid OrderId { get; private set; }

    public Order Order { get; private set; } = null!;

    public Guid? CustomerId { get; private set; }

    public Customer? Customer { get; private set; }

    /// <summary>The code the customer actually typed (null for automatic discounts).</summary>
    public string? Code { get; private set; }

    public decimal Amount { get; private set; }

    public DateTimeOffset RedeemedAt { get; private set; }
}
