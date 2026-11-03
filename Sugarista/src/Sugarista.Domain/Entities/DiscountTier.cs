using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

/// <summary>
/// One step of a tiered cart discount: once the eligible cart subtotal reaches
/// <see cref="ThresholdAmount"/>, the tier's percentage or fixed value applies.
/// The highest reached threshold wins.
/// </summary>
public class DiscountTier : BaseEntity
{
    private DiscountTier()
    {
    }

    public DiscountTier(Guid discountId, decimal thresholdAmount, DiscountValueKind valueKind, decimal value)
    {
        if (thresholdAmount <= 0)
        {
            throw new DomainException("A discount tier threshold must be greater than zero.");
        }

        if (value < 0)
        {
            throw new DomainException("A discount tier value cannot be negative.");
        }

        if (valueKind == DiscountValueKind.Percentage && value > 100)
        {
            throw new DomainException("A percentage discount tier cannot exceed 100.");
        }

        DiscountId = discountId;
        ThresholdAmount = thresholdAmount;
        ValueKind = valueKind;
        Value = value;
    }

    public Guid DiscountId { get; private set; }

    public Discount Discount { get; private set; } = null!;

    /// <summary>Minimum eligible cart subtotal for this tier to apply.</summary>
    public decimal ThresholdAmount { get; private set; }

    public DiscountValueKind ValueKind { get; private set; }

    /// <summary>Percentage (0-100) or a fixed amount, per <see cref="ValueKind"/>.</summary>
    public decimal Value { get; private set; }
}

/// <summary>Admin input for one tier (replaces the whole ladder in one call).</summary>
public sealed record DiscountTierInput(decimal ThresholdAmount, DiscountValueKind ValueKind, decimal Value);
