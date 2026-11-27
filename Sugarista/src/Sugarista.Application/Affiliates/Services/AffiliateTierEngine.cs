using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Affiliates.Services;

public sealed record AffiliateTierEvaluationResult(
    Guid AffiliateId,
    decimal Rolling30DayApprovedSales,
    Guid? PreviousTierId,
    Guid? NewTierId,
    string? NewTierName,
    bool TierChanged,
    bool IsCustomRateOverride,
    decimal EffectiveCommissionPercentage);

public sealed record AffiliateTierBatchEvaluationResult(
    int EvaluatedCount,
    int UpgradedCount,
    int DowngradedCount,
    int UnchangedCount,
    DateTimeOffset EvaluatedAt);

public interface IAffiliateTierEngine
{
    /// <summary>
    /// Immediately evaluates a single affiliate's rolling 30-day approved sales
    /// and upgrades or downgrades their TierId accordingly.
    /// </summary>
    Task<AffiliateTierEvaluationResult> EvaluateAffiliateTierAsync(
        Guid affiliateId,
        DateTimeOffset? asOf = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Evaluates all active affiliates in a single batched DB pass (no N+1)
    /// and applies upgrades/downgrades automatically.
    /// </summary>
    Task<AffiliateTierBatchEvaluationResult> EvaluateAllActiveAffiliatesAsync(
        DateTimeOffset? asOf = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets (or clears when null) a VIP fixed commission rate for an affiliate,
    /// overriding progressive tier rules.
    /// </summary>
    Task<AffiliateTierEvaluationResult> SetCustomCommissionRateAsync(
        Guid affiliateId,
        decimal? customCommissionRate,
        CancellationToken cancellationToken = default);
}

public sealed class AffiliateTierEngine : IAffiliateTierEngine
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IDateTime _dateTime;

    public AffiliateTierEngine(IApplicationDbContext dbContext, IDateTime dateTime)
    {
        _dbContext = dbContext;
        _dateTime = dateTime;
    }

    public async Task<AffiliateTierEvaluationResult> EvaluateAffiliateTierAsync(
        Guid affiliateId,
        DateTimeOffset? asOf = null,
        CancellationToken cancellationToken = default)
    {
        var now = asOf ?? _dateTime.UtcNow;
        var windowStart = now.AddDays(-AffiliateTierCalculator.RollingWindowDays);

        var affiliate = await _dbContext.Affiliates
            .FirstOrDefaultAsync(a => a.Id == affiliateId, cancellationToken)
            ?? throw new KeyNotFoundException("بازاریاب یافت نشد.");

        var tiers = await _dbContext.AffiliateTiers
            .AsNoTracking()
            .OrderByDescending(t => t.MinMonthlySales)
            .ToListAsync(cancellationToken);

        var rollingSales = await _dbContext.AffiliateCommissions
            .AsNoTracking()
            .Where(c => c.AffiliateId == affiliate.Id &&
                        (c.Status == AffiliateCommissionStatus.Approved || c.Status == AffiliateCommissionStatus.Paid) &&
                        c.CreatedAt >= windowStart &&
                        c.CreatedAt <= now)
            .SumAsync(c => (decimal?)c.EligibleAmount, cancellationToken) ?? 0m;

        var previousTierId = affiliate.TierId;
        var nextTier = AffiliateTierCalculator.ResolveTierForSales(rollingSales, tiers);
        var tierChanged = previousTierId != nextTier?.Id;

        if (tierChanged)
        {
            affiliate.AssignTier(nextTier?.Id);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var effective = AffiliateTierCalculator.ResolveEffectiveCommissionRate(affiliate, nextTier);

        return new AffiliateTierEvaluationResult(
            affiliate.Id,
            rollingSales,
            previousTierId,
            nextTier?.Id,
            nextTier?.Name,
            tierChanged,
            effective.Source == EffectiveCommissionSource.CustomVipOverride,
            effective.RatePercentage);
    }

    public async Task<AffiliateTierBatchEvaluationResult> EvaluateAllActiveAffiliatesAsync(
        DateTimeOffset? asOf = null,
        CancellationToken cancellationToken = default)
    {
        var now = asOf ?? _dateTime.UtcNow;
        var windowStart = now.AddDays(-AffiliateTierCalculator.RollingWindowDays);

        var tiers = await _dbContext.AffiliateTiers
            .AsNoTracking()
            .OrderByDescending(t => t.MinMonthlySales)
            .ToListAsync(cancellationToken);

        var tierById = tiers.ToDictionary(t => t.Id);

        var activeAffiliates = await _dbContext.Affiliates
            .Where(a => a.Status == AffiliateStatus.Active)
            .ToListAsync(cancellationToken);

        if (activeAffiliates.Count == 0)
        {
            return new AffiliateTierBatchEvaluationResult(0, 0, 0, 0, now);
        }

        var activeIds = activeAffiliates.Select(a => a.Id).ToList();

        // Single grouped SQL query across all active affiliates (zero N+1).
        var salesByAffiliate = await _dbContext.AffiliateCommissions
            .AsNoTracking()
            .Where(c => activeIds.Contains(c.AffiliateId) &&
                        (c.Status == AffiliateCommissionStatus.Approved || c.Status == AffiliateCommissionStatus.Paid) &&
                        c.CreatedAt >= windowStart &&
                        c.CreatedAt <= now)
            .GroupBy(c => c.AffiliateId)
            .Select(g => new { AffiliateId = g.Key, TotalSales = g.Sum(x => x.EligibleAmount) })
            .ToDictionaryAsync(x => x.AffiliateId, x => x.TotalSales, cancellationToken);

        var upgraded = 0;
        var downgraded = 0;
        var unchanged = 0;

        foreach (var affiliate in activeAffiliates)
        {
            var sales = salesByAffiliate.GetValueOrDefault(affiliate.Id, 0m);
            var previousTierId = affiliate.TierId;
            var nextTier = AffiliateTierCalculator.ResolveTierForSales(sales, tiers);

            if (previousTierId == nextTier?.Id)
            {
                unchanged++;
                continue;
            }

            var prevMinSales = previousTierId.HasValue && tierById.TryGetValue(previousTierId.Value, out var prevTier)
                ? prevTier.MinMonthlySales
                : -1m;
            var nextMinSales = nextTier?.MinMonthlySales ?? -1m;

            affiliate.AssignTier(nextTier?.Id);

            if (nextMinSales > prevMinSales)
            {
                upgraded++;
            }
            else
            {
                downgraded++;
            }
        }

        if (upgraded > 0 || downgraded > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return new AffiliateTierBatchEvaluationResult(
            activeAffiliates.Count,
            upgraded,
            downgraded,
            unchanged,
            now);
    }

    public async Task<AffiliateTierEvaluationResult> SetCustomCommissionRateAsync(
        Guid affiliateId,
        decimal? customCommissionRate,
        CancellationToken cancellationToken = default)
    {
        var affiliate = await _dbContext.Affiliates
            .Include(a => a.Tier)
            .FirstOrDefaultAsync(a => a.Id == affiliateId, cancellationToken)
            ?? throw new KeyNotFoundException("بازاریاب یافت نشد.");

        affiliate.SetCustomCommissionRate(customCommissionRate);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var now = _dateTime.UtcNow;
        var windowStart = now.AddDays(-AffiliateTierCalculator.RollingWindowDays);
        var rollingSales = await _dbContext.AffiliateCommissions
            .AsNoTracking()
            .Where(c => c.AffiliateId == affiliate.Id &&
                        (c.Status == AffiliateCommissionStatus.Approved || c.Status == AffiliateCommissionStatus.Paid) &&
                        c.CreatedAt >= windowStart &&
                        c.CreatedAt <= now)
            .SumAsync(c => (decimal?)c.EligibleAmount, cancellationToken) ?? 0m;

        var effective = AffiliateTierCalculator.ResolveEffectiveCommissionRate(affiliate, affiliate.Tier);

        return new AffiliateTierEvaluationResult(
            affiliate.Id,
            rollingSales,
            affiliate.TierId,
            affiliate.TierId,
            affiliate.Tier?.Name,
            TierChanged: false,
            IsCustomRateOverride: effective.Source == EffectiveCommissionSource.CustomVipOverride,
            EffectiveCommissionPercentage: effective.RatePercentage);
    }
}
