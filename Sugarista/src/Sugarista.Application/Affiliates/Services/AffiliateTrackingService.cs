using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Accounting.Services;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Domain.Entities;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Application.Affiliates.Services;

public sealed record RecordReferralClickResult(
    Guid ClickId,
    Guid TrackingToken,
    DateTimeOffset ExpiresAt,
    string TargetUrl,
    Guid AffiliateId,
    bool IsDuplicateClick = false);

public sealed record AffiliateCouponResolution(
    Guid CouponId,
    Guid AffiliateId,
    string Code,
    AffiliateDiscountType DiscountType,
    decimal DiscountValue,
    decimal? MaxDiscountAmount,
    decimal DiscountAmount,
    string AffiliateName);

public interface IAffiliateTrackingService
{
    Task<RecordReferralClickResult?> RecordClickAsync(
        string slug,
        string? subId,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default);

    Task<AffiliateCouponResolution?> TryResolveCouponAsync(
        string? code,
        decimal itemsSubtotal,
        Guid? customerId,
        bool throwOnInvalidCoupon = false,
        CancellationToken cancellationToken = default,
        string? customerPhone = null);

    Task<AffiliateCommission?> RecordOrderCommissionAsync(
        Order order,
        AffiliateOrderSource orderSource,
        Guid? couponAffiliateId,
        Guid? cookieTrackingToken,
        IReadOnlyDictionary<Guid, Guid> categoryByMenuItem,
        bool autoApprove = false,
        CancellationToken cancellationToken = default);
}

public sealed class AffiliateTrackingService : IAffiliateTrackingService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IDateTime _dateTime;
    private readonly IAffiliateTierEngine _tierEngine;
    private readonly IAffiliateNotificationService? _notificationService;
    private readonly IAccountingService? _accountingService;

    public AffiliateTrackingService(
        IApplicationDbContext dbContext,
        IDateTime dateTime,
        IAffiliateTierEngine tierEngine,
        IAffiliateNotificationService? notificationService = null,
        IAccountingService? accountingService = null)
    {
        _dbContext = dbContext;
        _dateTime = dateTime;
        _tierEngine = tierEngine;
        _notificationService = notificationService;
        _accountingService = accountingService;
    }

    public async Task<RecordReferralClickResult?> RecordClickAsync(
        string slug,
        string? subId,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return null;
        }

        var normalizedSlug = slug.Trim().ToLowerInvariant();
        var normalizedSubId = string.IsNullOrWhiteSpace(subId) ? null : subId.Trim().ToLowerInvariant();

        var candidates = await _dbContext.AffiliateLinks
            .Include(l => l.Affiliate)
            .Where(l => l.Slug == normalizedSlug && l.Affiliate.Status == AffiliateStatus.Active)
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0)
        {
            return null;
        }

        // Exact (slug, subId) match first, else default link with SubId == null, else first active link for slug.
        var link = (normalizedSubId is not null
                ? candidates.FirstOrDefault(l => l.SubId == normalizedSubId)
                : null)
            ?? candidates.FirstOrDefault(l => l.SubId == null)
            ?? candidates[0];

        var now = _dateTime.UtcNow;
        var targetUrl = string.IsNullOrWhiteSpace(link.TargetUrl) ? "/" : link.TargetUrl;
        var cleanIp = string.IsNullOrWhiteSpace(ipAddress) ? null : ipAddress.Trim();
        var cleanUa = string.IsNullOrWhiteSpace(userAgent) ? null : userAgent.Trim();

        // Anti-Fraud Rule 3: Deduplicate repeated clicks from the same IP + UserAgent within 1 hour.
        if (cleanIp is not null && cleanUa is not null)
        {
            var windowCutoff = now.Subtract(AffiliateAntiFraudEvaluator.DuplicateClickWindow);
            var recentDuplicate = await _dbContext.ReferralClicks
                .AsNoTracking()
                .Where(c => c.LinkId == link.Id &&
                            c.IpAddress == cleanIp &&
                            c.UserAgent == cleanUa &&
                            c.CreatedAt >= windowCutoff)
                .OrderByDescending(c => c.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (recentDuplicate is not null &&
                AffiliateAntiFraudEvaluator.IsDuplicateClickWithinWindow(recentDuplicate.CreatedAt, now))
            {
                return new RecordReferralClickResult(
                    recentDuplicate.Id,
                    recentDuplicate.TrackingToken,
                    recentDuplicate.ExpiresAt,
                    targetUrl,
                    link.AffiliateId,
                    IsDuplicateClick: true);
            }
        }

        var expiresAt = now.AddDays(AffiliateCommissionCalculator.DefaultCookieDays);
        var token = Guid.NewGuid();

        link.IncrementClickCount();
        var click = new ReferralClick(
            link.Id,
            token,
            expiresAt,
            cleanIp,
            cleanUa);

        _dbContext.ReferralClicks.Add(click);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new RecordReferralClickResult(
            click.Id,
            token,
            expiresAt,
            targetUrl,
            link.AffiliateId,
            IsDuplicateClick: false);
    }

    public async Task<AffiliateCouponResolution?> TryResolveCouponAsync(
        string? code,
        decimal itemsSubtotal,
        Guid? customerId,
        bool throwOnInvalidCoupon = false,
        CancellationToken cancellationToken = default,
        string? customerPhone = null)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        var normalizedCode = code.Trim().ToUpperInvariant();

        var coupon = await _dbContext.AffiliateCoupons
            .AsNoTracking()
            .Include(c => c.Affiliate)
                .ThenInclude(a => a.User)
            .FirstOrDefaultAsync(c => c.Code == normalizedCode, cancellationToken);

        if (coupon is null)
        {
            return null;
        }

        if (!coupon.IsActive || coupon.Affiliate.Status != AffiliateStatus.Active)
        {
            if (throwOnInvalidCoupon)
            {
                throw new DomainException("این کد معرف/تخفیف غیرفعال است.");
            }
            return null;
        }

        // Anti-Fraud Rule 1: Self-Referral Check (CustomerId + Normalized Phone)
        var selfReferral = AffiliateAntiFraudEvaluator.DetectSelfReferral(
            new AffiliateIdentitySnapshot(customerId, customerPhone),
            new AffiliateIdentitySnapshot(coupon.Affiliate.UserId, coupon.Affiliate.User?.PhoneNumber, coupon.Affiliate.User?.Email));

        if (selfReferral.IsSelfReferral)
        {
            if (throwOnInvalidCoupon)
            {
                throw new DomainException("امکان استفاده از کد معرف خودتان وجود ندارد.");
            }
            return null;
        }

        var phoneVariants = AffiliateAntiFraudEvaluator.BuildPhoneLookupVariants(customerPhone);

        // Anti-Fraud Rule 2: First-Order-Only check across BOTH CustomerId and CustomerPhone history
        if (coupon.IsFirstOrderOnly && (customerId.HasValue || phoneVariants.Count > 0))
        {
            var hasPriorPaidOrder = await _dbContext.Orders
                .AsNoTracking()
                .AnyAsync(o =>
                    o.Status == OrderStatus.Paid &&
                    ((customerId.HasValue && o.CustomerId == customerId.Value) ||
                     (phoneVariants.Count > 0 && phoneVariants.Contains(o.CustomerPhone))),
                    cancellationToken);

            if (hasPriorPaidOrder)
            {
                if (throwOnInvalidCoupon)
                {
                    throw new DomainException("این کد معرف فقط برای سفارش اول مشتریان جدید قابل استفاده است.");
                }
                return null;
            }
        }

        if (coupon.UsageLimitPerUser.HasValue && (customerId.HasValue || phoneVariants.Count > 0))
        {
            var usedCount = await _dbContext.OrderDiscounts
                .AsNoTracking()
                .CountAsync(
                    od => od.Code == normalizedCode &&
                          od.Order.Status != OrderStatus.Cancelled &&
                          od.Order.Status != OrderStatus.Failed &&
                          ((customerId.HasValue && od.Order.CustomerId == customerId.Value) ||
                           (phoneVariants.Count > 0 && phoneVariants.Contains(od.Order.CustomerPhone))),
                    cancellationToken);

            if (usedCount >= coupon.UsageLimitPerUser.Value)
            {
                if (throwOnInvalidCoupon)
                {
                    throw new DomainException("سقف مجاز استفاده از این کد معرف برای این مشتری تکمیل شده است.");
                }
                return null;
            }
        }

        var discountAmount = AffiliateCommissionCalculator.CalculateCouponDiscount(coupon, itemsSubtotal);

        return new AffiliateCouponResolution(
            coupon.Id,
            coupon.AffiliateId,
            coupon.Code,
            coupon.DiscountType,
            coupon.DiscountValue,
            coupon.MaxDiscountAmount,
            discountAmount,
            coupon.Affiliate.User?.PersianName ?? coupon.Code);
    }

    public async Task<AffiliateCommission?> RecordOrderCommissionAsync(
        Order order,
        AffiliateOrderSource orderSource,
        Guid? couponAffiliateId,
        Guid? cookieTrackingToken,
        IReadOnlyDictionary<Guid, Guid> categoryByMenuItem,
        bool autoApprove = false,
        CancellationToken cancellationToken = default)
    {
        Guid? cookieAffiliateId = null;

        if (!couponAffiliateId.HasValue && cookieTrackingToken.HasValue && cookieTrackingToken.Value != Guid.Empty)
        {
            var now = _dateTime.UtcNow;
            var click = await _dbContext.ReferralClicks
                .AsNoTracking()
                .Include(c => c.Link)
                    .ThenInclude(l => l.Affiliate)
                .FirstOrDefaultAsync(c => c.TrackingToken == cookieTrackingToken.Value, cancellationToken);

            if (click is not null &&
                !click.IsExpired(now) &&
                click.Link.Affiliate.Status == AffiliateStatus.Active)
            {
                cookieAffiliateId = click.Link.AffiliateId;
            }
        }

        var attribution = AffiliateCommissionCalculator.ResolveAttribution(couponAffiliateId, cookieAffiliateId);
        if (!attribution.AffiliateId.HasValue)
        {
            return null;
        }

        var affiliate = await _dbContext.Affiliates
            .Include(a => a.Tier)
            .Include(a => a.User)
            .FirstOrDefaultAsync(
                a => a.Id == attribution.AffiliateId.Value && a.Status == AffiliateStatus.Active,
                cancellationToken);

        if (affiliate is null)
        {
            return null;
        }

        // Idempotency: never create duplicate commissions for the same (Affiliate, Order).
        var exists = await _dbContext.AffiliateCommissions
            .AsNoTracking()
            .AnyAsync(c => c.AffiliateId == affiliate.Id && c.OrderId == order.Id, cancellationToken);

        if (exists)
        {
            return null;
        }

        var lineInputs = order.Lines
            .Select(line => new AffiliateCommissionLineInput(
                line.MenuItemId,
                categoryByMenuItem.GetValueOrDefault(line.MenuItemId),
                line.LineTotal))
            .ToList();

        var categoryIds = lineInputs
            .Select(l => l.CategoryId)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        var categoryRates = categoryIds.Count == 0
            ? new Dictionary<Guid, decimal>()
            : await _dbContext.CategoryCommissions
                .AsNoTracking()
                .Where(cc => categoryIds.Contains(cc.CategoryId))
                .ToDictionaryAsync(cc => cc.CategoryId, cc => cc.CommissionPercentage, cancellationToken);

        var categoryNames = categoryIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _dbContext.Categories
                .AsNoTracking()
                .Where(c => categoryIds.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);

        var calc = AffiliateCommissionCalculator.CalculateOrderCommission(
            affiliate,
            affiliate.Tier,
            lineInputs,
            order.DiscountTotal,
            categoryRates);

        var snapshotJson = AffiliateCommissionSnapshot
            .Build(order, affiliate, calc, categoryNames, _dateTime.UtcNow)
            .ToJson();

        // Anti-Fraud Rule 1: Self-Referral check — log as Rejected for admin audit & NEVER credit balance.
        var selfReferral = AffiliateAntiFraudEvaluator.DetectSelfReferral(
            new AffiliateIdentitySnapshot(order.CustomerId, order.CustomerPhone),
            new AffiliateIdentitySnapshot(affiliate.UserId, affiliate.User?.PhoneNumber, affiliate.User?.Email));

        if (selfReferral.IsSelfReferral)
        {
            var rejected = new AffiliateCommission(
                affiliate.Id,
                order.Id,
                orderSource,
                order.TotalAmount,
                calc.EligibleAmount,
                calc.CommissionAmount,
                AffiliateCommissionStatus.Pending,
                snapshotJson);

            rejected.Reject(selfReferral.Reason ?? "خودمعرفی (Self-Referral)");
            _dbContext.AffiliateCommissions.Add(rejected);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return rejected;
        }

        var initialStatus = autoApprove
            ? AffiliateCommissionStatus.Approved
            : AffiliateCommissionStatus.Pending;

        var commission = new AffiliateCommission(
            affiliate.Id,
            order.Id,
            orderSource,
            order.TotalAmount,
            calc.EligibleAmount,
            calc.CommissionAmount,
            initialStatus,
            snapshotJson);

        _dbContext.AffiliateCommissions.Add(commission);

        if (autoApprove && calc.CommissionAmount > 0m)
        {
            affiliate.CreditCashBalance(calc.CommissionAmount);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (autoApprove)
        {
            if (_accountingService is not null && calc.CommissionAmount > 0m)
            {
                await _accountingService.PostAffiliateCommissionApprovedAsync(
                    commission,
                    order.TrackingCode,
                    affiliate.User?.PersianName ?? "همکار فروش",
                    cancellationToken);
            }

            await _tierEngine.EvaluateAffiliateTierAsync(affiliate.Id, cancellationToken: cancellationToken);
        }

        if (_notificationService is not null && calc.CommissionAmount > 0m)
        {
            var affiliateName = affiliate.User?.PersianName ?? "همکار گرامی";
            if (autoApprove)
            {
                await _notificationService.NotifyCommissionApprovedAsync(
                    affiliate.User?.PhoneNumber,
                    affiliateName,
                    order.TrackingCode,
                    calc.CommissionAmount,
                    affiliate.CashBalance,
                    cancellationToken);
            }
            else
            {
                await _notificationService.NotifyNewOrderPendingCommissionAsync(
                    affiliate.User?.PhoneNumber,
                    affiliateName,
                    order.TrackingCode,
                    calc.CommissionAmount,
                    cancellationToken);
            }
        }

        return commission;
    }
}
