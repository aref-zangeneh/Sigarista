using Microsoft.EntityFrameworkCore;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Discounts.Engine;

public interface IDiscountEngine
{
    Task<DiscountEvaluationResult> EvaluateAsync(
        DiscountEvaluationContext context,
        CancellationToken cancellationToken = default);

    Task RecordRedemptionsAsync(
        Guid orderId,
        Guid? customerId,
        IReadOnlyCollection<DiscountApplication> applications,
        CancellationToken cancellationToken = default);
}

public sealed class DiscountEngine : IDiscountEngine
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IReadOnlyDictionary<DiscountType, IDiscountEvaluator> _evaluators;
    private readonly ILogger<DiscountEngine> _logger;

    public DiscountEngine(
        IApplicationDbContext dbContext,
        IEnumerable<IDiscountEvaluator> evaluators,
        ILogger<DiscountEngine> logger)
    {
        _dbContext = dbContext;
        _evaluators = evaluators.ToDictionary(e => e.Type);
        _logger = logger;
    }

    public async Task<DiscountEvaluationResult> EvaluateAsync(
        DiscountEvaluationContext context,
        CancellationToken cancellationToken = default)
    {
        var itemsSubtotal = context.Lines.Sum(line => line.LineTotal);
        var requestedCode = Discount.NormalizeCode(context.Code);
        var rejections = new List<DiscountRejection>();
        var applications = new List<DiscountApplication>();

        var candidates = await _dbContext.Discounts
            .AsNoTracking()
            .Include(discount => discount.MenuItems)
            .Include(discount => discount.Categories)
            .Include(discount => discount.Schedules)
            .Include(discount => discount.Tiers)
            .Where(discount => discount.IsActive)
            .ToListAsync(cancellationToken);

        var candidateIds = candidates.Select(discount => discount.Id).ToList();

        var totalUses = candidateIds.Count == 0
            ? new Dictionary<Guid, int>()
            : await _dbContext.DiscountRedemptions
                .Where(redemption => candidateIds.Contains(redemption.DiscountId))
                .GroupBy(redemption => redemption.DiscountId)
                .Select(group => new { DiscountId = group.Key, Count = group.Count() })
                .ToDictionaryAsync(x => x.DiscountId, x => x.Count, cancellationToken);

        var customerUses = context.CustomerId is { } customerId && candidateIds.Count > 0
            ? await _dbContext.DiscountRedemptions
                .Where(redemption => candidateIds.Contains(redemption.DiscountId) && redemption.CustomerId == customerId)
                .GroupBy(redemption => redemption.DiscountId)
                .Select(group => new { DiscountId = group.Key, Count = group.Count() })
                .ToDictionaryAsync(x => x.DiscountId, x => x.Count, cancellationToken)
            : new Dictionary<Guid, int>();

        // A typed code must resolve to something, even if it is currently unusable,
        // so the customer gets a reason rather than a silent no-op.
        if (requestedCode is not null)
        {
            var matched = candidates.FirstOrDefault(discount => discount.Code == requestedCode);
            if (matched is null)
            {
                var stored = await _dbContext.Discounts
                    .AsNoTracking()
                    .FirstOrDefaultAsync(discount => discount.Code == requestedCode, cancellationToken);

                rejections.Add(stored is null
                    ? new DiscountRejection(null, requestedCode, DiscountRejectionReason.NotFound, "این کد تخفیف وجود ندارد.")
                    : new DiscountRejection(stored.Id, requestedCode, DiscountRejectionReason.Inactive, "این کد تخفیف در حال حاضر فعال نیست."));
            }
        }

        var pending = context.Lines
            .Select(line => new PendingLine(line, line.LineTotal))
            .ToList();

        var stackableByDiscount = new Dictionary<Guid, bool>();

        var ordered = candidates
            .Where(discount => discount.IsAutomatic || (requestedCode is not null && discount.Code == requestedCode))
            .OrderByDescending(discount => discount.Priority)
            .ThenByDescending(discount => discount.Value)
            .ToList();

        foreach (var discount in ordered)
        {
            // Code handling: a coded discount only applies when that exact code was typed.
            if (!discount.IsAutomatic && discount.Code != requestedCode)
            {
                continue;
            }

            var rejection = Validate(discount, context, itemsSubtotal, totalUses, customerUses);
            if (rejection is not null)
            {
                rejections.Add(rejection);
                continue;
            }

            if (applications.Count > 0 &&
                (!discount.IsStackable || applications.Any(application => !stackableByDiscount[application.DiscountId])))
            {
                rejections.Add(new DiscountRejection(
                    discount.Id,
                    discount.Code,
                    DiscountRejectionReason.NotStackable,
                    "این تخفیف قابل ترکیب با تخفیف‌های دیگر نیست."));
                continue;
            }

            if (!_evaluators.TryGetValue(discount.Type, out var evaluator))
            {
                _logger.LogWarning("No evaluator registered for discount type {Type}.", discount.Type);
                continue;
            }

            var scoped = ScopeOf(discount, pending);
            if (discount.HasScopeEntries && scoped.Count == 0)
            {
                rejections.Add(new DiscountRejection(
                    discount.Id,
                    discount.Code,
                    DiscountRejectionReason.NoEligibleItems,
                    "هیچ کالای مشمولی در سبد خرید شما نیست."));
                continue;
            }

            var baseAmount = scoped.Sum(line => line.Remaining);
            var scopedInputs = scoped.Select(line => line.Input.UnitPrice > 0
                ? line.Input with { Quantity = (int)Math.Floor(line.Remaining / line.Input.UnitPrice) }
                : line.Input).ToList();
            var computation = evaluator.Evaluate(discount, context, baseAmount, scopedInputs);
            if (computation is null || computation.Amount <= 0)
            {
                continue;
            }

            var amount = Math.Min(computation.Amount, baseAmount);
            if (discount.MaxDiscountAmount is { } cap)
            {
                amount = Math.Min(amount, cap);
            }

            if (amount <= 0)
            {
                continue;
            }

            var shares = Allocate(computation.LineShares, scoped, amount);
            foreach (var share in shares)
            {
                var pendingLine = scoped.FirstOrDefault(line =>
                    line.Input.MenuItemId == share.MenuItemId &&
                    line.Input.SizeKey == share.SizeKey &&
                    line.Input.PayerKey == share.PayerKey);
                if (pendingLine is not null)
                {
                    pendingLine.Remaining = Math.Max(0, pendingLine.Remaining - share.Amount);
                }
            }

            applications.Add(new DiscountApplication(
                discount.Id,
                discount.Name,
                discount.Code,
                discount.Type,
                amount,
                shares,
                computation.Message));

            stackableByDiscount[discount.Id] = discount.IsStackable;
        }

        var discountTotal = applications.Sum(application => application.Amount);

        return new DiscountEvaluationResult(
            itemsSubtotal,
            discountTotal,
            applications,
            rejections);
    }

    public async Task RecordRedemptionsAsync(
        Guid orderId,
        Guid? customerId,
        IReadOnlyCollection<DiscountApplication> applications,
        CancellationToken cancellationToken = default)
    {
        if (applications.Count == 0)
        {
            return;
        }

        var existing = await _dbContext.DiscountRedemptions
            .Where(redemption => redemption.OrderId == orderId)
            .Select(redemption => redemption.DiscountId)
            .ToListAsync(cancellationToken);

        var existingSet = existing.ToHashSet();

        foreach (var application in applications)
        {
            if (existingSet.Contains(application.DiscountId))
            {
                continue;
            }

            _dbContext.DiscountRedemptions.Add(new DiscountRedemption(
                application.DiscountId,
                orderId,
                customerId,
                application.Code,
                application.Amount,
                DateTimeOffset.UtcNow));
        }
    }

    private static List<PendingLine> ScopeOf(Discount discount, List<PendingLine> pending)
    {
        if (!discount.HasScopeEntries)
        {
            return pending.Where(line => line.Remaining > 0).ToList();
        }

        var itemIds = discount.MenuItems.Select(item => (Guid?)item.MenuItemId).ToHashSet();
        var categoryIds = discount.Categories.Select(item => (Guid?)item.CategoryId).ToHashSet();

        return pending
            .Where(line => line.Remaining > 0 &&
                (itemIds.Contains(line.Input.MenuItemId) ||
                 (line.Input.CategoryId is not null && categoryIds.Contains(line.Input.CategoryId.Value))))
            .ToList();
    }

    private DiscountRejection? Validate(
        Discount discount,
        DiscountEvaluationContext context,
        decimal itemsSubtotal,
        IReadOnlyDictionary<Guid, int> totalUses,
        IReadOnlyDictionary<Guid, int> customerUses)
    {
        if (!discount.IsLive(context.EffectiveTime))
        {
            if (discount.StartsAt.HasValue && context.EffectiveTime < discount.StartsAt.Value)
            {
                return new DiscountRejection(discount.Id, discount.Code, DiscountRejectionReason.NotStarted, "این کد تخفیف هنوز فعال نشده است.");
            }

            return new DiscountRejection(discount.Id, discount.Code, DiscountRejectionReason.Expired, "این کد تخفیف منقضی شده است.");
        }

        if (!discount.MatchesSchedule(context.EffectiveTime, context.TimeZoneId))
        {
            return new DiscountRejection(discount.Id, discount.Code, DiscountRejectionReason.OutsideSchedule, "این تخفیف در این ساعت یا روز فعال نیست.");
        }

        if (discount.CustomerGroup != DiscountCustomerGroup.All &&
            !context.EffectiveGroups.Contains(discount.CustomerGroup))
        {
            return new DiscountRejection(discount.Id, discount.Code, DiscountRejectionReason.CustomerGroupMismatch, "این کد تخفیف برای شما قابل استفاده نیست.");
        }

        if (itemsSubtotal < discount.MinCartAmount)
        {
            return new DiscountRejection(discount.Id, discount.Code, DiscountRejectionReason.MinCartNotMet,
                $"حداقل مبلغ سبد برای این تخفیف {discount.MinCartAmount} است.");
        }

        if (discount.MaxTotalUses is { } maxTotal &&
            totalUses.TryGetValue(discount.Id, out var used) && used >= maxTotal)
        {
            return new DiscountRejection(discount.Id, discount.Code, DiscountRejectionReason.TotalUsesExceeded, "ظرفیت این کد تخفیف تکمیل شده است.");
        }

        if (discount.MaxUsesPerCustomer is { } maxPer &&
            context.CustomerId is { } customerId &&
            customerUses.TryGetValue(discount.Id, out var usedByCustomer) && usedByCustomer >= maxPer)
        {
            return new DiscountRejection(discount.Id, discount.Code, DiscountRejectionReason.CustomerUsesExceeded, "شما قبلاً از این کد استفاده کرده‌اید.");
        }

        return null;
    }

    private static IReadOnlyCollection<DiscountLineShare> Allocate(
        IReadOnlyCollection<DiscountLineShare>? explicitShares,
        List<PendingLine> scoped,
        decimal amount)
    {
        if (explicitShares is { Count: > 0 })
        {
            return explicitShares
                .Select(share => new DiscountLineShare(
                    share.MenuItemId,
                    share.SizeKey,
                    share.PayerKey,
                    Math.Min(share.Amount, scoped.FirstOrDefault(line =>
                        line.Input.MenuItemId == share.MenuItemId &&
                        line.Input.SizeKey == share.SizeKey &&
                        line.Input.PayerKey == share.PayerKey)?.Remaining ?? share.Amount)))
                .ToList();
        }

        var totalRemaining = scoped.Sum(line => line.Remaining);
        if (totalRemaining <= 0)
        {
            return [];
        }

        var shares = new List<DiscountLineShare>();
        var allocated = 0m;

        for (var i = 0; i < scoped.Count; i++)
        {
            var line = scoped[i];
            decimal share = i == scoped.Count - 1
                ? amount - allocated
                : Math.Round(amount * line.Remaining / totalRemaining, 2, MidpointRounding.AwayFromZero);

            share = Math.Max(0, Math.Min(share, line.Remaining));
            allocated += share;

            shares.Add(new DiscountLineShare(line.Input.MenuItemId, line.Input.SizeKey, line.Input.PayerKey, share));
        }

        return shares;
    }

    private sealed class PendingLine
    {
        public PendingLine(DiscountLineInput input, decimal remaining)
        {
            Input = input;
            Remaining = remaining;
        }

        public DiscountLineInput Input { get; }
        public decimal Remaining { get; set; }
    }
}
