using Microsoft.EntityFrameworkCore;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Discounts.Security;

/// <summary>How much of a discount belongs to one category (null = whole order).</summary>
public sealed record DiscountScopeAmount(Guid? CategoryId, decimal Amount);

/// <summary>
/// Writes one <see cref="OrderDiscount"/> row per discount/category and enforces
/// the customer's remaining daily allowance while doing so. Every benefit path
/// (engine code, tier, group, birthday, cup, subscription, points, stamp card)
/// goes through this tracker, so reports and the cap always see the full picture.
/// </summary>
public sealed class OrderDiscountTracker
{
    private readonly Order _order;
    private readonly Guid? _customerId;
    private readonly DbSet<OrderDiscount>? _orderDiscounts;
    private decimal? _remainingCap;

    public OrderDiscountTracker(
        Order order,
        Guid? customerId,
        decimal? remainingCap,
        DbSet<OrderDiscount>? orderDiscounts = null)
    {
        _order = order;
        _customerId = customerId;
        _remainingCap = remainingCap;
        _orderDiscounts = orderDiscounts;
    }

    /// <summary>Total amount actually applied through this tracker.</summary>
    public decimal AppliedTotal { get; private set; }

    /// <summary>Remaining allowance (null = unlimited).</summary>
    public decimal? RemainingCap => _remainingCap;

    /// <summary>
    /// Applies a benefit to the order, clamped by the remaining daily allowance
    /// and the order total, and records the matching <see cref="OrderDiscount"/>
    /// rows. Returns the amount actually applied (0 when nothing was left).
    /// </summary>
    public decimal Apply(
        OrderDiscountKind kind,
        string name,
        decimal amount,
        Guid? discountId = null,
        string? code = null,
        IReadOnlyCollection<DiscountScopeAmount>? scopes = null)
    {
        if (amount <= 0)
        {
            return 0m;
        }

        var applied = amount;

        if (_remainingCap is { } cap)
        {
            applied = Math.Min(applied, cap);
        }

        applied = Math.Min(applied, _order.TotalAmount);
        applied = Math.Round(applied, 2, MidpointRounding.AwayFromZero);

        if (applied <= 0)
        {
            return 0m;
        }

        _order.ApplyDiscount(applied);

        foreach (var (categoryId, rowAmount) in SplitByCategory(applied, scopes))
        {
            var discount = new OrderDiscount(
                _order.Id,
                _customerId,
                kind,
                name,
                rowAmount,
                discountId,
                code,
                categoryId);

            _order.Discounts.Add(discount);

            // New rows must be added to the set explicitly: an existing order is
            // already tracked, so a child discovered only through the navigation
            // is attached as an existing row and would produce an UPDATE (which
            // fails) instead of an INSERT.
            _orderDiscounts?.Add(discount);
        }

        AppliedTotal += applied;

        if (_remainingCap is { } remaining)
        {
            _remainingCap = Math.Max(0m, remaining - applied);
        }

        return applied;
    }

    /// <summary>
    /// Splits an applied amount across categories proportionally to the scoped
    /// amounts, rounding every row to 2 decimals and pushing the rounding
    /// remainder into the last row so the rows always sum to the exact amount.
    /// </summary>
    public static IReadOnlyList<(Guid? CategoryId, decimal Amount)> SplitByCategory(
        decimal amount,
        IReadOnlyCollection<DiscountScopeAmount>? scopes)
    {
        if (amount <= 0)
        {
            return [];
        }

        if (scopes is null || scopes.Count == 0)
        {
            return [(null, amount)];
        }

        var grouped = scopes
            .Where(scope => scope.Amount > 0)
            .GroupBy(scope => scope.CategoryId)
            .Select(group => new DiscountScopeAmount(group.Key, group.Sum(scope => scope.Amount)))
            .ToList();

        if (grouped.Count == 0)
        {
            return [(null, amount)];
        }

        if (grouped.Count == 1)
        {
            return [(grouped[0].CategoryId, amount)];
        }

        var totalScope = grouped.Sum(scope => scope.Amount);
        var rows = new List<(Guid? CategoryId, decimal Amount)>(grouped.Count);
        var allocated = 0m;

        for (var index = 0; index < grouped.Count; index++)
        {
            var scope = grouped[index];
            var isLast = index == grouped.Count - 1;
            var share = isLast
                ? amount - allocated
                : Math.Round(amount * scope.Amount / totalScope, 2, MidpointRounding.AwayFromZero);

            if (share <= 0 && !isLast)
            {
                continue;
            }

            rows.Add((scope.CategoryId, share));
            allocated += share;
        }

        return rows.Count > 0 ? rows : [(null, amount)];
    }
}
