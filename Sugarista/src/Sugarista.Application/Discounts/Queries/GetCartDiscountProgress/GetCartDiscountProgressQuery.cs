using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.Discounts.Engine;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Discounts.Queries.GetCartDiscountProgress;

public record GetCartDiscountProgressQuery(
    IReadOnlyCollection<DiscountLineInput> Lines,
    DateTimeOffset? Now = null) : IRequest<CartDiscountProgressDto>;

public record CartTierProgressDto(
    Guid DiscountId,
    string Name,
    decimal? AppliedThresholdAmount,
    decimal AppliedAmount,
    DiscountValueKind? AppliedValueKind,
    decimal? AppliedValue,
    decimal? NextThresholdAmount,
    decimal? RemainingAmount,
    DiscountValueKind? NextValueKind,
    decimal? NextValue,
    int ProgressPercent);

public record CartDiscountProgressDto(
    decimal ItemsSubtotal,
    decimal AutomaticDiscountTotal,
    CartTierProgressDto? Tier);

public class GetCartDiscountProgressQueryHandler : IRequestHandler<GetCartDiscountProgressQuery, CartDiscountProgressDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IDiscountEngine _discountEngine;

    public GetCartDiscountProgressQueryHandler(
        IApplicationDbContext dbContext,
        IDiscountEngine discountEngine)
    {
        _dbContext = dbContext;
        _discountEngine = discountEngine;
    }

    public async Task<CartDiscountProgressDto> Handle(
        GetCartDiscountProgressQuery request,
        CancellationToken cancellationToken)
    {
        var lines = request.Lines;

        // Resolve missing categories so category-scoped discounts behave exactly
        // like checkout.
        if (lines.Any(line => line.CategoryId is null))
        {
            var unresolvedItemIds = lines
                .Where(line => line.CategoryId is null)
                .Select(line => line.MenuItemId)
                .Distinct()
                .ToList();

            var categoryByItemId = await _dbContext.MenuItems
                .AsNoTracking()
                .Where(item => unresolvedItemIds.Contains(item.Id))
                .Select(item => new { item.Id, item.CategoryId })
                .ToDictionaryAsync(item => item.Id, item => item.CategoryId, cancellationToken);

            lines = lines
                .Select(line => line.CategoryId is null && categoryByItemId.TryGetValue(line.MenuItemId, out var categoryId)
                    ? line with { CategoryId = categoryId }
                    : line)
                .ToList();
        }

        var subtotal = lines.Sum(line => line.LineTotal);

        // Reuse the engine for the automatic discount total so the cart's final
        // amount matches what checkout will actually charge.
        var evaluation = await _discountEngine.EvaluateAsync(
            new DiscountEvaluationContext(
                lines,
                0m,
                null,
                null,
                null,
                request.Now ?? DateTimeOffset.UtcNow,
                "online",
                false,
                "Asia/Tehran"),
            cancellationToken);

        var automaticDiscountTotal = evaluation.DiscountTotal;

        var now = request.Now ?? DateTimeOffset.UtcNow;

        // Pick the primary automatic tiered discount (highest priority).
        var candidates = await _dbContext.Discounts
            .AsNoTracking()
            .Include(discount => discount.Tiers)
            .Include(discount => discount.Schedules)
            .Include(discount => discount.MenuItems)
            .Include(discount => discount.Categories)
            .Where(discount => discount.IsActive && discount.Type == DiscountType.TieredCart)
            .ToListAsync(cancellationToken);

        var primary = candidates
            .Where(discount => discount.IsAutomatic)
            .Where(discount => discount.IsLive(now))
            .Where(discount => discount.MatchesSchedule(now))
            .Where(discount => discount.CustomerGroup == DiscountCustomerGroup.All)
            .Where(discount => subtotal >= discount.MinCartAmount)
            .Where(discount => discount.Tiers.Count > 0)
            .OrderByDescending(discount => discount.Priority)
            .ThenBy(discount => discount.Tiers.Min(tier => tier.ThresholdAmount))
            .FirstOrDefault();

        if (primary is null)
        {
            return new CartDiscountProgressDto(subtotal, automaticDiscountTotal, null);
        }

        // Eligible base for this discount (respects scope).
        var scoped = ScopeOf(primary, lines);
        var eligibleBase = scoped.Sum(line => line.LineTotal);

        var appliedTier = DiscountTierSelector.SelectTier(primary, eligibleBase);
        var nextTier = DiscountTierSelector.SelectNextTier(primary, eligibleBase);

        decimal appliedAmount = 0;
        if (appliedTier is not null)
        {
            appliedAmount = Math.Min(
                DiscountTierSelector.AmountFor(appliedTier, eligibleBase),
                primary.MaxDiscountAmount ?? decimal.MaxValue);
            appliedAmount = Math.Max(0, appliedAmount);
        }

        decimal? appliedThreshold = appliedTier?.ThresholdAmount;
        DiscountValueKind? appliedValueKind = appliedTier?.ValueKind;
        decimal? appliedValue = appliedTier?.Value;

        decimal? nextThreshold = nextTier?.ThresholdAmount;
        DiscountValueKind? nextValueKind = nextTier?.ValueKind;
        decimal? nextValue = nextTier?.Value;

        decimal? remaining = nextThreshold.HasValue
            ? Math.Max(0, nextThreshold.Value - eligibleBase)
            : null;

        int progress;
        if (nextTier is null)
        {
            progress = 100;
        }
        else if (appliedTier is null)
        {
            progress = eligibleBase <= 0
                ? 0
                : (int)Math.Clamp(Math.Round(eligibleBase / nextTier.ThresholdAmount * 100m), 0m, 99m);
        }
        else
        {
            var span = nextTier.ThresholdAmount - appliedTier.ThresholdAmount;
            if (span <= 0)
            {
                progress = 100;
            }
            else
            {
                progress = (int)Math.Clamp(
                    Math.Round((eligibleBase - appliedTier.ThresholdAmount) / span * 100m),
                    0m,
                    99m);
            }
        }

        var tier = new CartTierProgressDto(
            primary.Id,
            primary.Name,
            appliedThreshold,
            appliedAmount,
            appliedValueKind,
            appliedValue,
            nextThreshold,
            remaining,
            nextValueKind,
            nextValue,
            progress);

        return new CartDiscountProgressDto(subtotal, automaticDiscountTotal, tier);
    }

    private static List<DiscountLineInput> ScopeOf(Discount discount, IEnumerable<DiscountLineInput> lines)
    {
        var pending = lines.Where(line => line.LineTotal > 0).ToList();

        if (!discount.HasScopeEntries)
        {
            return pending;
        }

        var itemIds = discount.MenuItems.Select(item => (Guid?)item.MenuItemId).ToHashSet();
        var categoryIds = discount.Categories.Select(item => (Guid?)item.CategoryId).ToHashSet();

        return pending
            .Where(line =>
                itemIds.Contains(line.MenuItemId) ||
                (line.CategoryId is not null && categoryIds.Contains(line.CategoryId.Value)))
            .ToList();
    }
}
