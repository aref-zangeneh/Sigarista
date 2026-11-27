using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Affiliates.Services;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.Customers.Services;
using Sugarista.Domain.Entities;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Application.Affiliates.Portal;

public static partial class AffiliatePortalRules
{
    [GeneratedRegex(@"^IR\d{24}$", RegexOptions.Compiled)]
    private static partial Regex IranianShebaRegex();

    [GeneratedRegex(@"^[a-z0-9][a-z0-9\-_]{1,48}$", RegexOptions.Compiled)]
    private static partial Regex SlugRegex();

    public static string NormalizeAndValidateSheba(string? rawSheba)
    {
        if (string.IsNullOrWhiteSpace(rawSheba))
        {
            throw new DomainException("شماره شبا الزامی است.");
        }

        var ascii = AffiliateAntiFraudEvaluator.NormalizeDigits(rawSheba)
            .Replace(" ", string.Empty)
            .Replace("-", string.Empty)
            .Trim()
            .ToUpperInvariant();

        if (!ascii.StartsWith("IR", StringComparison.Ordinal))
        {
            ascii = "IR" + ascii;
        }

        if (!IranianShebaRegex().IsMatch(ascii))
        {
            throw new DomainException("فرمت شماره شبا معتبر نیست (باید IR به همراه ۲۴ رقم باشد).");
        }

        return ascii;
    }

    public static string NormalizeSlug(string? rawSlug, string fallbackPrefix = "aff")
    {
        var ascii = AffiliateAntiFraudEvaluator.NormalizeDigits(rawSlug ?? string.Empty)
            .Trim()
            .ToLowerInvariant();
        ascii = Regex.Replace(ascii, @"[^a-z0-9\-_]+", "-").Trim('-');
        if (ascii.Length < 2 || !SlugRegex().IsMatch(ascii))
        {
            return $"{fallbackPrefix}-{Guid.NewGuid().ToString("N")[..6]}";
        }
        return ascii.Length > 48 ? ascii[..48] : ascii;
    }

    public static string NormalizeSubId(string? rawSubId)
    {
        if (string.IsNullOrWhiteSpace(rawSubId))
        {
            throw new DomainException("شناسه کمپین (Sub-ID) الزامی است.");
        }

        var ascii = AffiliateAntiFraudEvaluator.NormalizeDigits(rawSubId)
            .Trim()
            .ToLowerInvariant();
        ascii = Regex.Replace(ascii, @"[^a-z0-9\-_]+", "-").Trim('-');
        if (ascii.Length == 0 || ascii.Length > 64)
        {
            throw new DomainException("شناسه کمپین (Sub-ID) باید شامل حروف انگلیسی یا عدد باشد (مثلاً story1).");
        }

        return ascii;
    }

    public static decimal ComputeConversionRate(long totalClicks, int successfulOrders)
    {
        if (totalClicks <= 0 || successfulOrders <= 0)
        {
            return 0m;
        }

        return Math.Round((successfulOrders * 100m) / totalClicks, 1);
    }

    public static (decimal BonusAmount, decimal TotalCredit) ComputeCreditConversion(
        decimal cashAmount,
        decimal bonusPercentage)
    {
        if (cashAmount <= 0m)
        {
            return (0m, 0m);
        }

        var clampedPct = Math.Clamp(bonusPercentage, 0m, 100m);
        var bonus = Math.Floor(cashAmount * clampedPct / 100m);
        return (bonus, cashAmount + bonus);
    }
}

public sealed record AffiliateDailyStatDto(
    string Date,
    int OrdersCount,
    decimal SalesAmount,
    decimal CommissionAmount,
    int ClicksCount);

public sealed record AffiliatePortalLinkDto(
    Guid Id,
    string Slug,
    string? SubId,
    string TargetUrl,
    long ClickCount,
    string FullUrl,
    string QrSvgUrl,
    string QrPngUrl);

public sealed record AffiliatePortalCouponDto(
    Guid Id,
    string Code,
    AffiliateDiscountType DiscountType,
    decimal DiscountValue,
    decimal? MaxDiscountAmount,
    bool IsFirstOrderOnly,
    bool IsActive);

public sealed record AffiliatePortalPayoutDto(
    Guid Id,
    decimal Amount,
    AffiliatePayoutType PayoutType,
    AffiliatePayoutStatus Status,
    string? TrackingCode,
    string? ReceiptImageUrl,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ProcessedAt);

public sealed record AffiliatePortalMediaAssetDto(
    Guid Id,
    string Title,
    AffiliateMediaAssetType AssetType,
    string? FileUrl,
    string? CaptionText,
    string? ThumbnailUrl);

public sealed record AffiliatePortalDashboardDto(
    bool IsAuthenticated,
    string? CustomerName,
    string? PhoneNumber,
    bool HasAffiliateAccount,
    Guid? AffiliateId,
    AffiliateStatus? Status,
    string? TierName,
    decimal EffectiveCommissionPercentage,
    decimal CreditBonusPercentage,
    decimal MinPayoutAmount,
    string? ShebaNumber,
    string? CardNumber,
    long TotalClicks,
    int SuccessfulOrdersCount,
    decimal ConversionRate,
    decimal PendingCommissionAmount,
    decimal CashBalance,
    decimal WalletCreditBalance,
    IReadOnlyList<AffiliateDailyStatDto> DailyStats30d,
    IReadOnlyList<AffiliatePortalLinkDto> Links,
    IReadOnlyList<AffiliatePortalCouponDto> Coupons,
    IReadOnlyList<AffiliatePortalPayoutDto> Payouts,
    IReadOnlyList<AffiliatePortalMediaAssetDto> MediaKit);

public sealed record ConvertCashToCreditResultDto(
    decimal ConvertedCashAmount,
    decimal BonusPercentage,
    decimal BonusAmount,
    decimal TotalCreditGranted,
    decimal RemainingCashBalance,
    decimal NewWalletCreditBalance,
    decimal CafeWalletBalance);

public interface IAffiliatePortalService
{
    Task<AffiliatePortalDashboardDto> GetDashboardAsync(
        Guid customerId,
        string publicOrigin,
        CancellationToken cancellationToken = default);

    Task<AffiliatePortalDashboardDto> ApplyAsync(
        Guid customerId,
        string shebaNumber,
        string? cardNumber,
        string? preferredSlug,
        string publicOrigin,
        CancellationToken cancellationToken = default);

    Task<AffiliatePortalLinkDto> CreateCampaignLinkAsync(
        Guid customerId,
        string subId,
        string? targetUrl,
        string publicOrigin,
        CancellationToken cancellationToken = default);

    Task<ConvertCashToCreditResultDto> ConvertCashToCafeCreditAsync(
        Guid customerId,
        decimal amount,
        CancellationToken cancellationToken = default);

    Task<AffiliatePortalPayoutDto> RequestCashPayoutAsync(
        Guid customerId,
        decimal amount,
        CancellationToken cancellationToken = default);

    Task UpdateBankInfoAsync(
        Guid customerId,
        string shebaNumber,
        string? cardNumber,
        CancellationToken cancellationToken = default);
}

public sealed class AffiliatePortalService : IAffiliatePortalService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IDateTime _dateTime;

    public AffiliatePortalService(IApplicationDbContext dbContext, IDateTime dateTime)
    {
        _dbContext = dbContext;
        _dateTime = dateTime;
    }

    public async Task<AffiliatePortalDashboardDto> GetDashboardAsync(
        Guid customerId,
        string publicOrigin,
        CancellationToken cancellationToken = default)
    {
        var customer = await _dbContext.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == customerId, cancellationToken);

        if (customer is null)
        {
            return EmptyUnauthenticated();
        }

        var settings = await _dbContext.SiteSettings
            .AsNoTracking()
            .OrderBy(s => s.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var minPayoutAmount = settings?.AffiliateMinPayoutAmount ?? 500_000m;
        var defaultCreditBonus = settings?.AffiliateDefaultCreditBonusPercent ?? 20m;

        var affiliate = await _dbContext.Affiliates
            .AsNoTracking()
            .Include(a => a.Tier)
            .FirstOrDefaultAsync(a => a.UserId == customer.Id, cancellationToken);

        if (affiliate is null)
        {
            return new AffiliatePortalDashboardDto(
                IsAuthenticated: true,
                CustomerName: customer.PersianName,
                PhoneNumber: customer.PhoneNumber,
                HasAffiliateAccount: false,
                AffiliateId: null,
                Status: null,
                TierName: null,
                EffectiveCommissionPercentage: 0m,
                CreditBonusPercentage: defaultCreditBonus,
                MinPayoutAmount: minPayoutAmount,
                ShebaNumber: null,
                CardNumber: null,
                TotalClicks: 0,
                SuccessfulOrdersCount: 0,
                ConversionRate: 0m,
                PendingCommissionAmount: 0m,
                CashBalance: 0m,
                WalletCreditBalance: 0m,
                DailyStats30d: [],
                Links: [],
                Coupons: [],
                Payouts: [],
                MediaKit: []);
        }

        var origin = publicOrigin.TrimEnd('/');
        var links = await _dbContext.AffiliateLinks
            .AsNoTracking()
            .Where(l => l.AffiliateId == affiliate.Id)
            .OrderBy(l => l.SubId != null)
            .ThenByDescending(l => l.ClickCount)
            .ToListAsync(cancellationToken);

        var totalClicks = links.Sum(l => l.ClickCount);
        var linkIds = links.Select(l => l.Id).ToList();

        var commissions = await _dbContext.AffiliateCommissions
            .AsNoTracking()
            .Where(c => c.AffiliateId == affiliate.Id)
            .ToListAsync(cancellationToken);

        var successfulOrders = commissions.Count(c =>
            c.Status is AffiliateCommissionStatus.Approved or AffiliateCommissionStatus.Paid);

        var pendingCommission = commissions
            .Where(c => c.Status == AffiliateCommissionStatus.Pending)
            .Sum(c => c.CommissionAmount);

        var conversionRate = AffiliatePortalRules.ComputeConversionRate(totalClicks, successfulOrders);

        // 30-day daily stats series (UTC date buckets)
        var now = _dateTime.UtcNow;
        var startDay = now.UtcDateTime.Date.AddDays(-29);
        var windowStart = new DateTimeOffset(startDay, TimeSpan.Zero);

        var clicksByDay = linkIds.Count == 0
            ? new Dictionary<DateTime, int>()
            : (await _dbContext.ReferralClicks
                .AsNoTracking()
                .Where(c => linkIds.Contains(c.LinkId) && c.CreatedAt >= windowStart)
                .Select(c => c.CreatedAt)
                .ToListAsync(cancellationToken))
                .GroupBy(dt => dt.UtcDateTime.Date)
                .ToDictionary(g => g.Key, g => g.Count());

        var commissionsByDay = commissions
            .Where(c => c.CreatedAt >= windowStart &&
                        c.Status is AffiliateCommissionStatus.Approved or AffiliateCommissionStatus.Paid or AffiliateCommissionStatus.Pending)
            .GroupBy(c => c.CreatedAt.UtcDateTime.Date)
            .ToDictionary(
                g => g.Key,
                g => new
                {
                    Orders = g.Count(),
                    Sales = g.Sum(x => x.EligibleAmount),
                    Commission = g.Sum(x => x.CommissionAmount),
                });

        var dailyStats = new List<AffiliateDailyStatDto>(30);
        for (var i = 0; i < 30; i++)
        {
            var day = startDay.AddDays(i);
            commissionsByDay.TryGetValue(day, out var comm);
            clicksByDay.TryGetValue(day, out var clickCnt);
            dailyStats.Add(new AffiliateDailyStatDto(
                day.ToString("yyyy-MM-dd"),
                comm?.Orders ?? 0,
                comm?.Sales ?? 0m,
                comm?.Commission ?? 0m,
                clickCnt));
        }

        var coupons = await _dbContext.AffiliateCoupons
            .AsNoTracking()
            .Where(c => c.AffiliateId == affiliate.Id)
            .OrderByDescending(c => c.IsActive)
            .Select(c => new AffiliatePortalCouponDto(
                c.Id,
                c.Code,
                c.DiscountType,
                c.DiscountValue,
                c.MaxDiscountAmount,
                c.IsFirstOrderOnly,
                c.IsActive))
            .ToListAsync(cancellationToken);

        var payouts = await _dbContext.AffiliatePayouts
            .AsNoTracking()
            .Where(p => p.AffiliateId == affiliate.Id)
            .OrderByDescending(p => p.CreatedAt)
            .Take(25)
            .Select(p => new AffiliatePortalPayoutDto(
                p.Id,
                p.Amount,
                p.PayoutType,
                p.Status,
                p.TrackingCode,
                p.ReceiptImageUrl,
                p.CreatedAt,
                p.ProcessedAt))
            .ToListAsync(cancellationToken);

        var mediaKit = await _dbContext.AffiliateMediaAssets
            .AsNoTracking()
            .Where(m => m.IsActive)
            .OrderBy(m => m.DisplayOrder)
            .ThenByDescending(m => m.CreatedAt)
            .Select(m => new AffiliatePortalMediaAssetDto(
                m.Id,
                m.Title,
                m.AssetType,
                m.FileUrl,
                m.CaptionText,
                m.ThumbnailUrl))
            .ToListAsync(cancellationToken);

        var effectiveRate = AffiliateTierCalculator.ResolveEffectiveCommissionRate(affiliate, affiliate.Tier);
        var creditBonusPct = affiliate.Tier is not null && affiliate.Tier.CreditBonusPercentage > 0m
            ? affiliate.Tier.CreditBonusPercentage
            : defaultCreditBonus;

        return new AffiliatePortalDashboardDto(
            IsAuthenticated: true,
            CustomerName: customer.PersianName,
            PhoneNumber: customer.PhoneNumber,
            HasAffiliateAccount: true,
            AffiliateId: affiliate.Id,
            Status: affiliate.Status,
            TierName: affiliate.Tier?.Name ?? "پایه",
            EffectiveCommissionPercentage: effectiveRate.RatePercentage,
            CreditBonusPercentage: creditBonusPct,
            MinPayoutAmount: minPayoutAmount,
            ShebaNumber: affiliate.ShebaNumber,
            CardNumber: affiliate.CardNumber,
            TotalClicks: totalClicks,
            SuccessfulOrdersCount: successfulOrders,
            ConversionRate: conversionRate,
            PendingCommissionAmount: pendingCommission,
            CashBalance: affiliate.CashBalance,
            WalletCreditBalance: affiliate.WalletCreditBalance,
            DailyStats30d: dailyStats,
            Links: links.Select(l => MapLink(l, origin)).ToList(),
            Coupons: coupons,
            Payouts: payouts,
            MediaKit: mediaKit);
    }

    public async Task<AffiliatePortalDashboardDto> ApplyAsync(
        Guid customerId,
        string shebaNumber,
        string? cardNumber,
        string? preferredSlug,
        string publicOrigin,
        CancellationToken cancellationToken = default)
    {
        var customer = await _dbContext.Customers
            .FirstOrDefaultAsync(c => c.Id == customerId, cancellationToken)
            ?? throw new KeyNotFoundException("مشتری یافت نشد.");

        var existing = await _dbContext.Affiliates
            .FirstOrDefaultAsync(a => a.UserId == customer.Id, cancellationToken);

        if (existing is not null)
        {
            throw new DomainException("شما قبلاً درخواست همکاری در فروش ثبت کرده‌اید.");
        }

        var normalizedSheba = AffiliatePortalRules.NormalizeAndValidateSheba(shebaNumber);
        var baseTier = await _dbContext.AffiliateTiers
            .AsNoTracking()
            .OrderBy(t => t.MinMonthlySales)
            .FirstOrDefaultAsync(cancellationToken);

        var affiliate = new Affiliate(
            customer.Id,
            tierId: baseTier?.Id,
            status: AffiliateStatus.Pending,
            shebaNumber: normalizedSheba,
            cardNumber: cardNumber);

        _dbContext.Affiliates.Add(affiliate);

        var slug = AffiliatePortalRules.NormalizeSlug(preferredSlug);
        var slugTaken = await _dbContext.AffiliateLinks
            .AsNoTracking()
            .AnyAsync(l => l.Slug == slug, cancellationToken);
        if (slugTaken)
        {
            slug = $"{slug}-{Guid.NewGuid().ToString("N")[..4]}";
        }

        _dbContext.AffiliateLinks.Add(new AffiliateLink(affiliate.Id, slug, "/"));
        await _dbContext.SaveChangesAsync(cancellationToken);

        return await GetDashboardAsync(customer.Id, publicOrigin, cancellationToken);
    }

    public async Task<AffiliatePortalLinkDto> CreateCampaignLinkAsync(
        Guid customerId,
        string subId,
        string? targetUrl,
        string publicOrigin,
        CancellationToken cancellationToken = default)
    {
        var affiliate = await _dbContext.Affiliates
            .Include(a => a.Links)
            .FirstOrDefaultAsync(a => a.UserId == customerId, cancellationToken)
            ?? throw new KeyNotFoundException("حساب همکاری در فروش یافت نشد.");

        if (affiliate.Status != AffiliateStatus.Active)
        {
            throw new DomainException("حساب همکاری در فروش شما هنوز فعال نشده است.");
        }

        var normalizedSubId = AffiliatePortalRules.NormalizeSubId(subId);
        var baseSlug = affiliate.Links.FirstOrDefault(l => l.SubId == null)?.Slug
            ?? affiliate.Links.FirstOrDefault()?.Slug
            ?? AffiliatePortalRules.NormalizeSlug(null);

        var duplicate = affiliate.Links.FirstOrDefault(l =>
            l.Slug == baseSlug && string.Equals(l.SubId, normalizedSubId, StringComparison.OrdinalIgnoreCase));

        if (duplicate is not null)
        {
            return MapLink(duplicate, publicOrigin.TrimEnd('/'));
        }

        var safeTarget = string.IsNullOrWhiteSpace(targetUrl) ? "/" : targetUrl.Trim();
        var link = new AffiliateLink(affiliate.Id, baseSlug, safeTarget, normalizedSubId);
        _dbContext.AffiliateLinks.Add(link);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapLink(link, publicOrigin.TrimEnd('/'));
    }

    public async Task<ConvertCashToCreditResultDto> ConvertCashToCafeCreditAsync(
        Guid customerId,
        decimal amount,
        CancellationToken cancellationToken = default)
    {
        if (amount <= 0m)
        {
            throw new DomainException("مبلغ تبدیل باید بزرگ‌تر از صفر باشد.");
        }

        var affiliate = await _dbContext.Affiliates
            .Include(a => a.Tier)
            .FirstOrDefaultAsync(a => a.UserId == customerId, cancellationToken)
            ?? throw new KeyNotFoundException("حساب همکاری در فروش یافت نشد.");

        if (affiliate.Status != AffiliateStatus.Active)
        {
            throw new DomainException("حساب همکاری در فروش شما فعال نیست.");
        }

        var settings = await _dbContext.SiteSettings
            .AsNoTracking()
            .OrderBy(s => s.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var bonusPct = affiliate.Tier is not null && affiliate.Tier.CreditBonusPercentage > 0m
            ? affiliate.Tier.CreditBonusPercentage
            : (settings?.AffiliateDefaultCreditBonusPercent ?? 20m);

        var (bonusAmount, totalCredit) = AffiliatePortalRules.ComputeCreditConversion(amount, bonusPct);

        affiliate.DebitCashBalance(amount);
        affiliate.CreditWalletCreditBalance(totalCredit);

        var payout = new AffiliatePayout(affiliate.Id, amount, AffiliatePayoutType.WalletCredit);
        payout.Complete($"WALLET-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}", null, _dateTime.UtcNow);
        _dbContext.AffiliatePayouts.Add(payout);

        var cafeWallet = await WalletService.GetOrCreateAsync(_dbContext, customerId, cancellationToken);
        await WalletService.TopUpAsync(
            _dbContext,
            cafeWallet,
            totalCredit,
            $"تبدیل درآمد همکاری در فروش به اعتبار کافه ({amount:N0} + {bonusPct:0.##}٪ بونوس)",
            cancellationToken);

        return new ConvertCashToCreditResultDto(
            amount,
            bonusPct,
            bonusAmount,
            totalCredit,
            affiliate.CashBalance,
            affiliate.WalletCreditBalance,
            cafeWallet.Balance);
    }

    public async Task<AffiliatePortalPayoutDto> RequestCashPayoutAsync(
        Guid customerId,
        decimal amount,
        CancellationToken cancellationToken = default)
    {
        var affiliate = await _dbContext.Affiliates
            .FirstOrDefaultAsync(a => a.UserId == customerId, cancellationToken)
            ?? throw new KeyNotFoundException("حساب همکاری در فروش یافت نشد.");

        if (affiliate.Status != AffiliateStatus.Active)
        {
            throw new DomainException("حساب همکاری در فروش شما فعال نیست.");
        }

        if (string.IsNullOrWhiteSpace(affiliate.ShebaNumber))
        {
            throw new DomainException("لطفاً ابتدا شماره شبا به نام خود را ثبت کنید.");
        }

        var settings = await _dbContext.SiteSettings
            .AsNoTracking()
            .OrderBy(s => s.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var minPayout = settings?.AffiliateMinPayoutAmount ?? 500_000m;
        if (amount < minPayout)
        {
            throw new DomainException($"حداقل مبلغ قابل برداشت نقدی {minPayout:N0} تومان است.");
        }

        affiliate.DebitCashBalance(amount);

        var payout = new AffiliatePayout(affiliate.Id, amount, AffiliatePayoutType.Cash);
        _dbContext.AffiliatePayouts.Add(payout);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new AffiliatePortalPayoutDto(
            payout.Id,
            payout.Amount,
            payout.PayoutType,
            payout.Status,
            payout.TrackingCode,
            payout.ReceiptImageUrl,
            payout.CreatedAt,
            payout.ProcessedAt);
    }

    public async Task UpdateBankInfoAsync(
        Guid customerId,
        string shebaNumber,
        string? cardNumber,
        CancellationToken cancellationToken = default)
    {
        var affiliate = await _dbContext.Affiliates
            .FirstOrDefaultAsync(a => a.UserId == customerId, cancellationToken)
            ?? throw new KeyNotFoundException("حساب همکاری در فروش یافت نشد.");

        var normalizedSheba = AffiliatePortalRules.NormalizeAndValidateSheba(shebaNumber);
        affiliate.UpdateBankInfo(normalizedSheba, cardNumber);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static AffiliatePortalLinkDto MapLink(AffiliateLink link, string origin)
    {
        var fullUrl = string.IsNullOrWhiteSpace(link.SubId)
            ? $"{origin}/r/{Uri.EscapeDataString(link.Slug)}"
            : $"{origin}/r/{Uri.EscapeDataString(link.Slug)}?src={Uri.EscapeDataString(link.SubId)}";

        return new AffiliatePortalLinkDto(
            link.Id,
            link.Slug,
            link.SubId,
            link.TargetUrl,
            link.ClickCount,
            fullUrl,
            $"/api/affiliates/links/{link.Id}/qr?format=svg&size=512",
            $"/api/affiliates/links/{link.Id}/qr?format=png&size=1024");
    }

    public static AffiliatePortalDashboardDto EmptyUnauthenticated() =>
        new(
            IsAuthenticated: false,
            CustomerName: null,
            PhoneNumber: null,
            HasAffiliateAccount: false,
            AffiliateId: null,
            Status: null,
            TierName: null,
            EffectiveCommissionPercentage: 0m,
            CreditBonusPercentage: 20m,
            MinPayoutAmount: 500_000m,
            ShebaNumber: null,
            CardNumber: null,
            TotalClicks: 0,
            SuccessfulOrdersCount: 0,
            ConversionRate: 0m,
            PendingCommissionAmount: 0m,
            CashBalance: 0m,
            WalletCreditBalance: 0m,
            DailyStats30d: [],
            Links: [],
            Coupons: [],
            Payouts: [],
            MediaKit: []);
}
