using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sugarista.Api.Authorization;
using Sugarista.Application.Accounting.Services;
using Sugarista.Application.Affiliates.Portal;
using Sugarista.Application.Affiliates.Services;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Domain.Constants;
using Sugarista.Domain.Entities;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Api.Controllers;

public sealed record AdminCreateAffiliateRequest(
    string CustomerPhone,
    string? CustomerName,
    Guid? TierId,
    decimal? CustomCommissionRate,
    string? ShebaNumber,
    string? PreferredSlug,
    string? InitialCouponCode,
    decimal? InitialCouponPercent);

public sealed record AdminUpdateAffiliateStatusRequest(AffiliateStatus Status);

public sealed record AdminUpdateAffiliateTierRateRequest(
    Guid? TierId,
    decimal? CustomCommissionRate);

public sealed record AdminUpsertAffiliateTierRequest(
    Guid? Id,
    string Name,
    decimal MinMonthlySales,
    decimal CommissionPercentage,
    decimal CreditBonusPercentage);

public sealed record AdminUpsertCategoryCommissionRequest(
    Guid CategoryId,
    decimal? CommissionPercentage);

public sealed record AdminCreateAffiliateCouponRequest(
    string Code,
    AffiliateDiscountType DiscountType,
    decimal DiscountValue,
    decimal? MaxDiscountAmount,
    bool IsFirstOrderOnly,
    int? UsageLimitPerUser,
    int? MaxTotalUsages = null,
    DateTimeOffset? StartsAt = null,
    DateTimeOffset? ExpiresAt = null,
    bool IsActive = true);

public sealed record AdminCompletePayoutRequest(
    string? TrackingCode,
    string? ReceiptImageUrl);

public sealed record AdminUpsertMediaAssetRequest(
    Guid? Id,
    string Title,
    AffiliateMediaAssetType AssetType,
    string? FileUrl,
    string? CaptionText,
    string? ThumbnailUrl,
    int DisplayOrder = 0,
    bool IsActive = true);

/// <summary>
/// Full Admin Control Panel for the Cafe Affiliate Marketing System:
/// affiliate approvals, VIP custom rates, tiers, category commissions,
/// coupons, cash payouts (with tracking code & receipt upload), and Media Kit.
/// </summary>
[Authorize]
[Route("api/admin/affiliates")]
public class AdminAffiliatesController : ApiControllerBase
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IAffiliateTierEngine _tierEngine;
    private readonly IAffiliateNotificationService _notificationService;
    private readonly IAccountingService _accountingService;
    private readonly IDateTime _dateTime;

    public AdminAffiliatesController(
        IApplicationDbContext dbContext,
        IAffiliateTierEngine tierEngine,
        IAffiliateNotificationService notificationService,
        IAccountingService accountingService,
        IDateTime dateTime)
    {
        _dbContext = dbContext;
        _tierEngine = tierEngine;
        _notificationService = notificationService;
        _accountingService = accountingService;
        _dateTime = dateTime;
    }

    [HttpGet("summary")]
    [RequirePermission(AdminPermissions.Affiliates.View)]
    public async Task<ActionResult<object>> GetSummary(CancellationToken cancellationToken = default)
    {
        var totalAffiliates = await _dbContext.Affiliates.AsNoTracking().CountAsync(cancellationToken);
        var pendingAffiliates = await _dbContext.Affiliates.AsNoTracking().CountAsync(a => a.Status == AffiliateStatus.Pending, cancellationToken);
        var activeAffiliates = await _dbContext.Affiliates.AsNoTracking().CountAsync(a => a.Status == AffiliateStatus.Active, cancellationToken);
        var totalClicks = await _dbContext.AffiliateLinks.AsNoTracking().SumAsync(l => (long?)l.ClickCount, cancellationToken) ?? 0L;
        var totalApprovedCommission = await _dbContext.AffiliateCommissions.AsNoTracking()
            .Where(c => c.Status == AffiliateCommissionStatus.Approved || c.Status == AffiliateCommissionStatus.Paid)
            .SumAsync(c => (decimal?)c.CommissionAmount, cancellationToken) ?? 0m;
        var totalCashLiability = await _dbContext.Affiliates.AsNoTracking()
            .SumAsync(a => (decimal?)a.CashBalance, cancellationToken) ?? 0m;
        var openPayoutsCount = await _dbContext.AffiliatePayouts.AsNoTracking()
            .CountAsync(p => p.Status == AffiliatePayoutStatus.Requested || p.Status == AffiliatePayoutStatus.Processing, cancellationToken);

        return Ok(new
        {
            totalAffiliates,
            pendingAffiliates,
            activeAffiliates,
            totalClicks,
            totalApprovedCommission,
            totalCashLiability,
            openPayoutsCount,
        });
    }

    [HttpGet]
    [RequirePermission(AdminPermissions.Affiliates.View)]
    public async Task<ActionResult<object>> GetAffiliates(
        [FromQuery] AffiliateStatus? status = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Affiliates
            .AsNoTracking()
            .Include(a => a.User)
            .Include(a => a.Tier)
            .Include(a => a.Links)
            .Include(a => a.Coupons)
            .AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(a => a.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = AffiliateAntiFraudEvaluator.NormalizeDigits(search).Trim().ToLowerInvariant();
            query = query.Where(a =>
                a.User.PersianName.ToLower().Contains(term) ||
                a.User.PhoneNumber.Contains(term) ||
                (a.ShebaNumber != null && a.ShebaNumber.ToLower().Contains(term)));
        }

        var affiliates = await query
            .OrderByDescending(a => a.Status == AffiliateStatus.Pending)
            .ThenByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);

        var ids = affiliates.Select(a => a.Id).ToList();
        var commStats = ids.Count == 0
            ? new Dictionary<Guid, (int Orders, decimal Sales, decimal Pending, decimal Approved)>()
            : (await _dbContext.AffiliateCommissions
                .AsNoTracking()
                .Where(c => ids.Contains(c.AffiliateId))
                .GroupBy(c => c.AffiliateId)
                .Select(g => new
                {
                    AffiliateId = g.Key,
                    Orders = g.Count(x => x.Status == AffiliateCommissionStatus.Approved || x.Status == AffiliateCommissionStatus.Paid),
                    Sales = g.Where(x => x.Status == AffiliateCommissionStatus.Approved || x.Status == AffiliateCommissionStatus.Paid).Sum(x => x.EligibleAmount),
                    Pending = g.Where(x => x.Status == AffiliateCommissionStatus.Pending).Sum(x => x.CommissionAmount),
                    Approved = g.Where(x => x.Status == AffiliateCommissionStatus.Approved || x.Status == AffiliateCommissionStatus.Paid).Sum(x => x.CommissionAmount),
                })
                .ToListAsync(cancellationToken))
                .ToDictionary(x => x.AffiliateId, x => (x.Orders, x.Sales, x.Pending, x.Approved));

        var result = affiliates.Select(a =>
        {
            commStats.TryGetValue(a.Id, out var stat);
            var effective = AffiliateTierCalculator.ResolveEffectiveCommissionRate(a, a.Tier);
            return new
            {
                a.Id,
                a.UserId,
                customerName = a.User.PersianName,
                phoneNumber = a.User.PhoneNumber,
                a.Status,
                a.TierId,
                tierName = a.Tier?.Name ?? "پایه",
                a.CustomCommissionRate,
                effectiveCommissionPercentage = effective.RatePercentage,
                a.ShebaNumber,
                a.CardNumber,
                a.CashBalance,
                a.WalletCreditBalance,
                totalClicks = a.Links.Sum(l => l.ClickCount),
                ordersCount = stat.Orders,
                approvedSales = stat.Sales,
                pendingCommission = stat.Pending,
                approvedCommission = stat.Approved,
                linksCount = a.Links.Count,
                coupons = a.Coupons.Select(c => new
                {
                    c.Id,
                    c.Code,
                    c.DiscountType,
                    c.DiscountValue,
                    c.MaxDiscountAmount,
                    c.IsFirstOrderOnly,
                    c.UsageLimitPerUser,
                    c.MaxTotalUsages,
                    c.StartsAt,
                    c.ExpiresAt,
                    c.IsActive,
                }),
                a.CreatedAt,
            };
        });

        return Ok(result);
    }

    [HttpPost]
    [RequirePermission(AdminPermissions.Affiliates.Update)]
    public async Task<ActionResult<object>> CreateAffiliate(
        [FromBody] AdminCreateAffiliateRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var phone = AffiliateAntiFraudEvaluator.NormalizePhone(request.CustomerPhone);
            if (string.IsNullOrWhiteSpace(phone))
            {
                return Problem("شماره موبایل معتبر نیست.", statusCode: 400);
            }

            var variants = AffiliateAntiFraudEvaluator.BuildPhoneLookupVariants(phone);
            var customer = await _dbContext.Customers
                .FirstOrDefaultAsync(c => variants.Contains(c.PhoneNumber), cancellationToken);

            if (customer is null)
            {
                var name = string.IsNullOrWhiteSpace(request.CustomerName) ? phone : request.CustomerName.Trim();
                customer = new Customer(name, phone, "admin");
                _dbContext.Customers.Add(customer);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            var exists = await _dbContext.Affiliates.AnyAsync(a => a.UserId == customer.Id, cancellationToken);
            if (exists)
            {
                return Problem("برای این مشتری قبلاً حساب همکاری در فروش ثبت شده است.", statusCode: 400);
            }

            string? sheba = null;
            if (!string.IsNullOrWhiteSpace(request.ShebaNumber))
            {
                sheba = AffiliatePortalRules.NormalizeAndValidateSheba(request.ShebaNumber);
            }

            var tierId = request.TierId ?? (await _dbContext.AffiliateTiers
                .AsNoTracking()
                .OrderBy(t => t.MinMonthlySales)
                .Select(t => (Guid?)t.Id)
                .FirstOrDefaultAsync(cancellationToken));

            var affiliate = new Affiliate(
                customer.Id,
                tierId: tierId,
                status: AffiliateStatus.Active,
                shebaNumber: sheba,
                customCommissionRate: request.CustomCommissionRate);

            _dbContext.Affiliates.Add(affiliate);

            var slug = AffiliatePortalRules.NormalizeSlug(request.PreferredSlug);
            if (await _dbContext.AffiliateLinks.AnyAsync(l => l.Slug == slug, cancellationToken))
            {
                slug = $"{slug}-{Guid.NewGuid().ToString("N")[..4]}";
            }
            _dbContext.AffiliateLinks.Add(new AffiliateLink(affiliate.Id, slug, "/"));

            string? createdCouponCode = null;
            if (!string.IsNullOrWhiteSpace(request.InitialCouponCode) && (request.InitialCouponPercent ?? 0m) > 0m)
            {
                var coupon = new AffiliateCoupon(
                    affiliate.Id,
                    request.InitialCouponCode,
                    AffiliateDiscountType.Percentage,
                    request.InitialCouponPercent!.Value);
                _dbContext.AffiliateCoupons.Add(coupon);
                createdCouponCode = coupon.Code;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            await _notificationService.NotifyAccountApprovedAsync(
                customer.PhoneNumber,
                customer.PersianName,
                createdCouponCode,
                cancellationToken);

            return Ok(new { affiliate.Id, slug, createdCouponCode });
        }
        catch (DomainException exception)
        {
            return Problem(exception.Message, statusCode: 400);
        }
    }

    [HttpPatch("{id:guid}/status")]
    [RequirePermission(AdminPermissions.Affiliates.Update)]
    public async Task<ActionResult> UpdateStatus(
        Guid id,
        [FromBody] AdminUpdateAffiliateStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        var affiliate = await _dbContext.Affiliates
            .Include(a => a.User)
            .Include(a => a.Coupons)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        if (affiliate is null)
        {
            return NotFound();
        }

        var wasActive = affiliate.Status == AffiliateStatus.Active;
        affiliate.SetStatus(request.Status);
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (!wasActive && request.Status == AffiliateStatus.Active)
        {
            var firstCoupon = affiliate.Coupons.FirstOrDefault(c => c.IsActive)?.Code;
            await _notificationService.NotifyAccountApprovedAsync(
                affiliate.User?.PhoneNumber,
                affiliate.User?.PersianName ?? "همکار گرامی",
                firstCoupon,
                cancellationToken);
        }

        return NoContent();
    }

    [HttpPatch("{id:guid}/tier-rate")]
    [RequirePermission(AdminPermissions.Affiliates.Update)]
    public async Task<ActionResult> UpdateTierAndRate(
        Guid id,
        [FromBody] AdminUpdateAffiliateTierRateRequest request,
        CancellationToken cancellationToken = default)
    {
        var affiliate = await _dbContext.Affiliates
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        if (affiliate is null)
        {
            return NotFound();
        }

        try
        {
            affiliate.AssignTier(request.TierId);
            affiliate.SetCustomCommissionRate(request.CustomCommissionRate);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return NoContent();
        }
        catch (DomainException exception)
        {
            return Problem(exception.Message, statusCode: 400);
        }
    }

    [HttpPost("{id:guid}/evaluate-tier")]
    [RequirePermission(AdminPermissions.Affiliates.Update)]
    public async Task<ActionResult<AffiliateTierEvaluationResult>> EvaluateTier(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _tierEngine.EvaluateAffiliateTierAsync(id, cancellationToken: cancellationToken));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("{id:guid}/coupons")]
    [RequirePermission(AdminPermissions.Affiliates.Update)]
    public async Task<ActionResult<object>> CreateCoupon(
        Guid id,
        [FromBody] AdminCreateAffiliateCouponRequest request,
        CancellationToken cancellationToken = default)
    {
        var affiliateExists = await _dbContext.Affiliates.AnyAsync(a => a.Id == id, cancellationToken);
        if (!affiliateExists)
        {
            return NotFound();
        }

        try
        {
            var normalizedCode = request.Code.Trim().ToUpperInvariant();
            if (await _dbContext.AffiliateCoupons.AnyAsync(c => c.Code == normalizedCode, cancellationToken))
            {
                return Problem("این کد تخفیف قبلاً ثبت شده است.", statusCode: 400);
            }

            var coupon = new AffiliateCoupon(
                id,
                normalizedCode,
                request.DiscountType,
                request.DiscountValue,
                request.MaxDiscountAmount,
                request.IsFirstOrderOnly,
                request.UsageLimitPerUser,
                request.IsActive,
                request.MaxTotalUsages,
                request.StartsAt,
                request.ExpiresAt);

            _dbContext.AffiliateCoupons.Add(coupon);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return Ok(new { coupon.Id, coupon.Code });
        }
        catch (DomainException exception)
        {
            return Problem(exception.Message, statusCode: 400);
        }
    }

    [HttpPut("coupons/{couponId:guid}")]
    [RequirePermission(AdminPermissions.Affiliates.Update)]
    public async Task<ActionResult<object>> UpdateCoupon(
        Guid couponId,
        [FromBody] AdminCreateAffiliateCouponRequest request,
        CancellationToken cancellationToken = default)
    {
        var coupon = await _dbContext.AffiliateCoupons
            .FirstOrDefaultAsync(c => c.Id == couponId, cancellationToken);
        if (coupon is null) return NotFound();

        try
        {
            var normalizedCode = request.Code.Trim().ToUpperInvariant();
            if (await _dbContext.AffiliateCoupons.AnyAsync(c => c.Code == normalizedCode && c.Id != couponId, cancellationToken))
            {
                return Problem("این کد تخفیف قبلاً ثبت شده است.", statusCode: 400);
            }

            coupon.Update(
                normalizedCode,
                request.DiscountType,
                request.DiscountValue,
                request.MaxDiscountAmount,
                request.IsFirstOrderOnly,
                request.UsageLimitPerUser,
                request.IsActive,
                request.MaxTotalUsages,
                request.StartsAt,
                request.ExpiresAt);

            await _dbContext.SaveChangesAsync(cancellationToken);
            return Ok(new { coupon.Id, coupon.Code });
        }
        catch (DomainException exception)
        {
            return Problem(exception.Message, statusCode: 400);
        }
    }

    [HttpDelete("coupons/{couponId:guid}")]
    [RequirePermission(AdminPermissions.Affiliates.Update)]
    public async Task<ActionResult> DeleteCoupon(Guid couponId, CancellationToken cancellationToken = default)
    {
        var coupon = await _dbContext.AffiliateCoupons.FirstOrDefaultAsync(c => c.Id == couponId, cancellationToken);
        if (coupon is null) return NotFound();

        _dbContext.AffiliateCoupons.Remove(coupon);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    // ── Tiers Management ────────────────────────────────────────────────────

    [HttpGet("tiers")]
    [RequirePermission(AdminPermissions.Affiliates.View)]
    public async Task<ActionResult<object>> GetTiers(CancellationToken cancellationToken = default)
    {
        var tiers = await _dbContext.AffiliateTiers
            .AsNoTracking()
            .OrderBy(t => t.MinMonthlySales)
            .Select(t => new
            {
                t.Id,
                t.Name,
                t.MinMonthlySales,
                t.CommissionPercentage,
                t.CreditBonusPercentage,
                affiliatesCount = t.Affiliates.Count,
            })
            .ToListAsync(cancellationToken);

        return Ok(tiers);
    }

    [HttpPost("tiers")]
    [RequirePermission(AdminPermissions.Affiliates.Update)]
    public async Task<ActionResult<object>> UpsertTier(
        [FromBody] AdminUpsertAffiliateTierRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            AffiliateTier tier;
            if (request.Id.HasValue && request.Id.Value != Guid.Empty)
            {
                tier = await _dbContext.AffiliateTiers.FirstOrDefaultAsync(t => t.Id == request.Id.Value, cancellationToken)
                    ?? throw new KeyNotFoundException("سطح یافت نشد.");
                tier.Update(request.Name, request.MinMonthlySales, request.CommissionPercentage, request.CreditBonusPercentage);
            }
            else
            {
                tier = new AffiliateTier(request.Name, request.MinMonthlySales, request.CommissionPercentage, request.CreditBonusPercentage);
                _dbContext.AffiliateTiers.Add(tier);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            return Ok(new { tier.Id, tier.Name });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (DomainException exception)
        {
            return Problem(exception.Message, statusCode: 400);
        }
    }

    [HttpDelete("tiers/{id:guid}")]
    [RequirePermission(AdminPermissions.Affiliates.Update)]
    public async Task<ActionResult> DeleteTier(Guid id, CancellationToken cancellationToken = default)
    {
        var tier = await _dbContext.AffiliateTiers.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (tier is null) return NotFound();

        _dbContext.AffiliateTiers.Remove(tier);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    // ── Category Commissions ────────────────────────────────────────────────

    [HttpGet("category-commissions")]
    [RequirePermission(AdminPermissions.Affiliates.View)]
    public async Task<ActionResult<object>> GetCategoryCommissions(CancellationToken cancellationToken = default)
    {
        var categories = await _dbContext.Categories
            .AsNoTracking()
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.Name)
            .ToListAsync(cancellationToken);

        var rates = await _dbContext.CategoryCommissions
            .AsNoTracking()
            .ToDictionaryAsync(cc => cc.CategoryId, cc => cc.CommissionPercentage, cancellationToken);

        return Ok(categories.Select(c => new
        {
            categoryId = c.Id,
            categoryName = c.Name,
            commissionPercentage = rates.TryGetValue(c.Id, out var pct) ? (decimal?)pct : null,
        }));
    }

    [HttpPut("category-commissions")]
    [RequirePermission(AdminPermissions.Affiliates.Update)]
    public async Task<ActionResult> UpsertCategoryCommission(
        [FromBody] AdminUpsertCategoryCommissionRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var existing = await _dbContext.CategoryCommissions
                .FirstOrDefaultAsync(cc => cc.CategoryId == request.CategoryId, cancellationToken);

            if (!request.CommissionPercentage.HasValue)
            {
                if (existing is not null)
                {
                    _dbContext.CategoryCommissions.Remove(existing);
                    await _dbContext.SaveChangesAsync(cancellationToken);
                }
                return NoContent();
            }

            if (existing is null)
            {
                _dbContext.CategoryCommissions.Add(new CategoryCommission(request.CategoryId, request.CommissionPercentage.Value));
            }
            else
            {
                existing.SetCommissionPercentage(request.CommissionPercentage.Value);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            return NoContent();
        }
        catch (DomainException exception)
        {
            return Problem(exception.Message, statusCode: 400);
        }
    }

    // ── Payouts Management ──────────────────────────────────────────────────

    [HttpGet("payouts")]
    [RequirePermission(AdminPermissions.Affiliates.View)]
    public async Task<ActionResult<object>> GetPayouts(
        [FromQuery] AffiliatePayoutStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.AffiliatePayouts
            .AsNoTracking()
            .Include(p => p.Affiliate)
                .ThenInclude(a => a.User)
            .AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(p => p.Status == status.Value);
        }

        var items = await query
            .OrderByDescending(p => p.Status == AffiliatePayoutStatus.Requested || p.Status == AffiliatePayoutStatus.Processing)
            .ThenByDescending(p => p.CreatedAt)
            .Take(100)
            .Select(p => new
            {
                p.Id,
                p.AffiliateId,
                affiliateName = p.Affiliate.User.PersianName,
                phoneNumber = p.Affiliate.User.PhoneNumber,
                shebaNumber = p.Affiliate.ShebaNumber,
                p.Amount,
                p.PayoutType,
                p.Status,
                p.TrackingCode,
                p.ReceiptImageUrl,
                p.CreatedAt,
                p.ProcessedAt,
            })
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpPost("payouts/{id:guid}/complete")]
    [RequirePermission(AdminPermissions.Affiliates.Update)]
    public async Task<ActionResult> CompletePayout(
        Guid id,
        [FromBody] AdminCompletePayoutRequest request,
        CancellationToken cancellationToken = default)
    {
        var payout = await _dbContext.AffiliatePayouts
            .Include(p => p.Affiliate)
                .ThenInclude(a => a.User)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (payout is null) return NotFound();

        try
        {
            payout.Complete(request.TrackingCode, request.ReceiptImageUrl, _dateTime.UtcNow);
            await _dbContext.SaveChangesAsync(cancellationToken);

            if (payout.PayoutType == AffiliatePayoutType.Cash)
            {
                var affiliateName = payout.Affiliate.User?.PersianName ?? "همکار گرامی";
                await _accountingService.PostAffiliatePayoutCompletedAsync(
                    payout,
                    affiliateName,
                    cancellationToken);

                await _notificationService.NotifyPayoutCompletedAsync(
                    payout.Affiliate.User?.PhoneNumber,
                    affiliateName,
                    payout.Amount,
                    payout.TrackingCode,
                    cancellationToken);
            }

            return NoContent();
        }
        catch (DomainException exception)
        {
            return Problem(exception.Message, statusCode: 400);
        }
    }

    [HttpPost("payouts/{id:guid}/reject")]
    [RequirePermission(AdminPermissions.Affiliates.Update)]
    public async Task<ActionResult> RejectPayout(Guid id, CancellationToken cancellationToken = default)
    {
        var payout = await _dbContext.AffiliatePayouts
            .Include(p => p.Affiliate)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (payout is null) return NotFound();

        try
        {
            payout.Reject(_dateTime.UtcNow);
            if (payout.PayoutType == AffiliatePayoutType.Cash && payout.Amount > 0m)
            {
                // Refund cash balance when a requested bank payout is rejected.
                payout.Affiliate.CreditCashBalance(payout.Amount);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            return NoContent();
        }
        catch (DomainException exception)
        {
            return Problem(exception.Message, statusCode: 400);
        }
    }

    // ── Media Kit Management ────────────────────────────────────────────────

    [HttpGet("media-kit")]
    [RequirePermission(AdminPermissions.Affiliates.View)]
    public async Task<ActionResult<object>> GetMediaKit(CancellationToken cancellationToken = default)
    {
        var assets = await _dbContext.AffiliateMediaAssets
            .AsNoTracking()
            .OrderBy(m => m.DisplayOrder)
            .ThenByDescending(m => m.CreatedAt)
            .ToListAsync(cancellationToken);

        return Ok(assets);
    }

    [HttpPost("media-kit")]
    [RequirePermission(AdminPermissions.Affiliates.Update)]
    public async Task<ActionResult<object>> UpsertMediaAsset(
        [FromBody] AdminUpsertMediaAssetRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            AffiliateMediaAsset asset;
            if (request.Id.HasValue && request.Id.Value != Guid.Empty)
            {
                asset = await _dbContext.AffiliateMediaAssets.FirstOrDefaultAsync(m => m.Id == request.Id.Value, cancellationToken)
                    ?? throw new KeyNotFoundException("محتوا یافت نشد.");
                asset.Update(request.Title, request.AssetType, request.FileUrl, request.CaptionText, request.ThumbnailUrl, request.DisplayOrder, request.IsActive);
            }
            else
            {
                asset = new AffiliateMediaAsset(request.Title, request.AssetType, request.FileUrl, request.CaptionText, request.ThumbnailUrl, request.DisplayOrder, request.IsActive);
                _dbContext.AffiliateMediaAssets.Add(asset);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            return Ok(asset);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (DomainException exception)
        {
            return Problem(exception.Message, statusCode: 400);
        }
    }

    [HttpDelete("media-kit/{id:guid}")]
    [RequirePermission(AdminPermissions.Affiliates.Update)]
    public async Task<ActionResult> DeleteMediaAsset(Guid id, CancellationToken cancellationToken = default)
    {
        var asset = await _dbContext.AffiliateMediaAssets.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
        if (asset is null) return NotFound();

        _dbContext.AffiliateMediaAssets.Remove(asset);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}
