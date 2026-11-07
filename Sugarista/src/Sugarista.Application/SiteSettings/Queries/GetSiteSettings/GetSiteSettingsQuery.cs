using Sugarista.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sugarista.Application.Common.Options;
using Sugarista.Application.Orders;

namespace Sugarista.Application.SiteSettings.Queries.GetSiteSettings;

public record GetSiteSettingsQuery : IRequest<SiteSettingsDto>;

public record SiteSettingsDto
{
    public Guid Id { get; init; }
    public string CafeName { get; init; } = string.Empty;
    public string BrowserTitle { get; init; } = string.Empty;
    public string MetaDescription { get; init; } = string.Empty;
    public string Eyebrow { get; init; } = string.Empty;
    public string BrandTitle { get; init; } = string.Empty;
    public string BrandSubtitle { get; init; } = string.Empty;
    public string HeroBadgeText { get; init; } = string.Empty;
    public string HeroTitle { get; init; } = string.Empty;
    public string HeroDescription { get; init; } = string.Empty;
    public string HeroPrimaryButtonLabel { get; init; } = string.Empty;
    public string HeroSecondaryButtonLabel { get; init; } = string.Empty;
    public string HeroHours { get; init; } = string.Empty;
    public string HowToOrderLabel { get; init; } = string.Empty;
    public string HowToOrderTitle { get; init; } = string.Empty;
    public string HowToOrderStatus { get; init; } = string.Empty;
    public string StepOneTitle { get; init; } = string.Empty;
    public string StepOneDescription { get; init; } = string.Empty;
    public string StepTwoTitle { get; init; } = string.Empty;
    public string StepTwoDescription { get; init; } = string.Empty;
    public string StepThreeTitle { get; init; } = string.Empty;
    public string StepThreeDescription { get; init; } = string.Empty;
    public string QuickAccessLabel { get; init; } = string.Empty;
    public string QuickAccessTitle { get; init; } = string.Empty;
    public string QuickAccessDescription { get; init; } = string.Empty;
    public string PopularLabel { get; init; } = string.Empty;
    public string PopularTitle { get; init; } = string.Empty;
    public string PopularDescription { get; init; } = string.Empty;
    public string MenuLabel { get; init; } = string.Empty;
    public string MenuTitle { get; init; } = string.Empty;
    public string MenuToolbarHint { get; init; } = string.Empty;
    public string SearchPlaceholder { get; init; } = string.Empty;
    public string VisitLabel { get; init; } = string.Empty;
    public string VisitTitle { get; init; } = string.Empty;
    public string VisitDescription { get; init; } = string.Empty;
    public string ContactPhone { get; init; } = string.Empty;
    public string Address { get; init; } = string.Empty;
    public string BreakfastHours { get; init; } = string.Empty;
    public string TakeawayStatus { get; init; } = string.Empty;
    public string InstagramUrl { get; init; } = string.Empty;
    public string FooterText { get; init; } = string.Empty;
    public string FooterPopularLabel { get; init; } = string.Empty;
    public string FooterMenuLabel { get; init; } = string.Empty;
    public string FooterCallLabel { get; init; } = string.Empty;
    public bool IsOnlineSalesEnabled { get; init; } = true;
    public string OnlineSalesNotice { get; init; } = string.Empty;
    public string TimeZoneId { get; init; } = "Asia/Tehran";
    public int LightThemeStartHour { get; init; } = 6;
    public int DarkThemeStartHour { get; init; } = 20;
    public string ThemeMode { get; init; } = "auto";
    public bool AllowManualThemeOverride { get; init; } = true;
    public bool IsOnlineOrderingAvailable { get; init; } = true;
    public int WinBackDays { get; init; } = 30;
    public bool LoyaltyEnabled { get; init; }
    public decimal LoyaltyEarnTomanPerPoint { get; init; } = 10000m;
    public decimal LoyaltyRedeemTomanPerPoint { get; init; } = 1000m;
    public int LoyaltyPointsExpiryDays { get; init; } = 365;
    public bool StampCardEnabled { get; init; } = true;
    public int StampCardOrdersPerCard { get; init; } = 10;
    public decimal StampCardRewardPercent { get; init; } = 10m;
    public int StampCardRewardValidityDays { get; init; } = 30;
    public bool MembershipTiersEnabled { get; init; } = true;
    public int MembershipWindowDays { get; init; } = 365;
    public bool MembershipDowngradeEnabled { get; init; } = true;

    public bool WalletEnabled { get; init; } = true;
    public decimal WalletCashbackPercent { get; init; } = 2m;
    public decimal WalletCashbackMinOrderAmount { get; init; }
    public decimal WalletCashbackMaxPerOrderAmount { get; init; }
    public int WalletCashbackExpiryDays { get; init; }
    public int WalletPaymentMaxPercent { get; init; } = 100;

    public bool BirthdayDiscountEnabled { get; init; }
    public BirthdayDiscountMode BirthdayDiscountMode { get; init; } = BirthdayDiscountMode.Automatic;
    public decimal BirthdayDiscountPercent { get; init; }
    public int BirthdayDiscountWindowDays { get; init; }
    public string BirthdayGreetingMessage { get; init; } = string.Empty;
    public bool BirthdayEditableByCustomer { get; init; } = true;

    public bool PersonalCupDiscountEnabled { get; init; }
    public decimal PersonalCupDiscountAmount { get; init; }
    public string? PersonalCupCategoryIds { get; init; }
    public string PersonalCupDeliveryNote { get; init; } = string.Empty;

    public string SubscriptionRenewalPolicy { get; init; } = string.Empty;
    public string SubscriptionCancelRefundPolicy { get; init; } = string.Empty;
    public bool CustomDrinkBuilderEnabled { get; init; } = true;

    // ── pickup scheduling (TASK-012) ─────────────────────────────────────
    public bool PickupSchedulingEnabled { get; init; }
    public int PickupSlotMinutes { get; init; } = 15;
    public int PickupSlotCapacity { get; init; } = 5;
    public string PickupCapacityMode { get; init; } = "Orders";
    public int PickupMinLeadMinutes { get; init; } = 30;
    public int PickupMaxDaysAhead { get; init; } = 7;
    public int PickupCancelCutoffMinutes { get; init; } = 60;
    public bool PickupAutoRefundOnCancel { get; init; }
    public string PickupRefundPolicyNote { get; init; } = string.Empty;
    public bool DiscountAbuseProtectionEnabled { get; init; } = true;
    public int DiscountMaxFailedAttempts { get; init; } = 5;
    public int DiscountLockoutMinutes { get; init; } = 15;
    public decimal DiscountDailyCapPerCustomer { get; init; }
    public bool AbandonedCartReminderEnabled { get; init; } = true;
    public int AbandonedCartHours { get; init; } = 4;
    public bool AbandonedCartSmsEnabled { get; init; } = true;
    public bool AbandonedCartEmailEnabled { get; init; }
    public decimal AbandonedCartDiscountPercent { get; init; } = 10m;
    public int AbandonedCartCodeValidityHours { get; init; } = 48;
    public string AbandonedCartSmsTemplate { get; init; } = string.Empty;
    public string AbandonedCartEmailSubject { get; init; } = string.Empty;
    public string AbandonedCartEmailTemplate { get; init; } = string.Empty;
}

public class GetSiteSettingsQueryHandler : IRequestHandler<GetSiteSettingsQuery, SiteSettingsDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly PaymentOptions _paymentOptions;

    public GetSiteSettingsQueryHandler(
        IApplicationDbContext dbContext,
        IOptions<PaymentOptions> paymentOptions)
    {
        _dbContext = dbContext;
        _paymentOptions = paymentOptions.Value;
    }

    public async Task<SiteSettingsDto> Handle(GetSiteSettingsQuery request, CancellationToken cancellationToken)
    {
        var settings = await _dbContext.SiteSettings
            .AsNoTracking()
            .Select(settings => new SiteSettingsDto
            {
                Id = settings.Id,
                CafeName = settings.CafeName,
                BrowserTitle = settings.BrowserTitle,
                MetaDescription = settings.MetaDescription,
                Eyebrow = settings.Eyebrow,
                BrandTitle = settings.BrandTitle,
                BrandSubtitle = settings.BrandSubtitle,
                HeroBadgeText = settings.HeroBadgeText,
                HeroTitle = settings.HeroTitle,
                HeroDescription = settings.HeroDescription,
                HeroPrimaryButtonLabel = settings.HeroPrimaryButtonLabel,
                HeroSecondaryButtonLabel = settings.HeroSecondaryButtonLabel,
                HeroHours = settings.HeroHours,
                HowToOrderLabel = settings.HowToOrderLabel,
                HowToOrderTitle = settings.HowToOrderTitle,
                HowToOrderStatus = settings.HowToOrderStatus,
                StepOneTitle = settings.StepOneTitle,
                StepOneDescription = settings.StepOneDescription,
                StepTwoTitle = settings.StepTwoTitle,
                StepTwoDescription = settings.StepTwoDescription,
                StepThreeTitle = settings.StepThreeTitle,
                StepThreeDescription = settings.StepThreeDescription,
                QuickAccessLabel = settings.QuickAccessLabel,
                QuickAccessTitle = settings.QuickAccessTitle,
                QuickAccessDescription = settings.QuickAccessDescription,
                PopularLabel = settings.PopularLabel,
                PopularTitle = settings.PopularTitle,
                PopularDescription = settings.PopularDescription,
                MenuLabel = settings.MenuLabel,
                MenuTitle = settings.MenuTitle,
                MenuToolbarHint = settings.MenuToolbarHint,
                SearchPlaceholder = settings.SearchPlaceholder,
                VisitLabel = settings.VisitLabel,
                VisitTitle = settings.VisitTitle,
                VisitDescription = settings.VisitDescription,
                ContactPhone = settings.ContactPhone,
                Address = settings.Address,
                BreakfastHours = settings.BreakfastHours,
                TakeawayStatus = settings.TakeawayStatus,
                InstagramUrl = settings.InstagramUrl,
                FooterText = settings.FooterText,
                FooterPopularLabel = settings.FooterPopularLabel,
                FooterMenuLabel = settings.FooterMenuLabel,
                FooterCallLabel = settings.FooterCallLabel,
                IsOnlineSalesEnabled = settings.IsOnlineSalesEnabled,
                OnlineSalesNotice = settings.OnlineSalesNotice,
                TimeZoneId = settings.TimeZoneId,
                LightThemeStartHour = settings.LightThemeStartHour,
                DarkThemeStartHour = settings.DarkThemeStartHour,
                ThemeMode = settings.ThemeMode,
                AllowManualThemeOverride = settings.AllowManualThemeOverride,
                WinBackDays = settings.WinBackDays,
                LoyaltyEnabled = settings.LoyaltyEnabled,
                LoyaltyEarnTomanPerPoint = settings.LoyaltyEarnTomanPerPoint,
                LoyaltyRedeemTomanPerPoint = settings.LoyaltyRedeemTomanPerPoint,
                LoyaltyPointsExpiryDays = settings.LoyaltyPointsExpiryDays,
                StampCardEnabled = settings.StampCardEnabled,
                StampCardOrdersPerCard = settings.StampCardOrdersPerCard,
                StampCardRewardPercent = settings.StampCardRewardPercent,
                StampCardRewardValidityDays = settings.StampCardRewardValidityDays,
                MembershipTiersEnabled = settings.MembershipTiersEnabled,
                MembershipWindowDays = settings.MembershipWindowDays,
                MembershipDowngradeEnabled = settings.MembershipDowngradeEnabled,
                WalletEnabled = settings.WalletEnabled,
                WalletCashbackPercent = settings.WalletCashbackPercent,
                WalletCashbackMinOrderAmount = settings.WalletCashbackMinOrderAmount,
                WalletCashbackMaxPerOrderAmount = settings.WalletCashbackMaxPerOrderAmount,
                WalletCashbackExpiryDays = settings.WalletCashbackExpiryDays,
                WalletPaymentMaxPercent = settings.WalletPaymentMaxPercent,
                BirthdayDiscountEnabled = settings.BirthdayDiscountEnabled,
                BirthdayDiscountMode = settings.BirthdayDiscountMode,
                BirthdayDiscountPercent = settings.BirthdayDiscountPercent,
                BirthdayDiscountWindowDays = settings.BirthdayDiscountWindowDays,
                BirthdayGreetingMessage = settings.BirthdayGreetingMessage,
                BirthdayEditableByCustomer = settings.BirthdayEditableByCustomer,
                PersonalCupDiscountEnabled = settings.PersonalCupDiscountEnabled,
                PersonalCupDiscountAmount = settings.PersonalCupDiscountAmount,
                PersonalCupCategoryIds = settings.PersonalCupCategoryIds,
                PersonalCupDeliveryNote = settings.PersonalCupDeliveryNote,
                SubscriptionRenewalPolicy = settings.SubscriptionRenewalPolicy,
                SubscriptionCancelRefundPolicy = settings.SubscriptionCancelRefundPolicy,
                CustomDrinkBuilderEnabled = settings.CustomDrinkBuilderEnabled,
                PickupSchedulingEnabled = settings.PickupSchedulingEnabled,
                PickupSlotMinutes = settings.PickupSlotMinutes,
                PickupSlotCapacity = settings.PickupSlotCapacity,
                PickupCapacityMode = settings.PickupCapacityMode,
                PickupMinLeadMinutes = settings.PickupMinLeadMinutes,
                PickupMaxDaysAhead = settings.PickupMaxDaysAhead,
                PickupCancelCutoffMinutes = settings.PickupCancelCutoffMinutes,
                PickupAutoRefundOnCancel = settings.PickupAutoRefundOnCancel,
                PickupRefundPolicyNote = settings.PickupRefundPolicyNote,
                DiscountAbuseProtectionEnabled = settings.DiscountAbuseProtectionEnabled,
                DiscountMaxFailedAttempts = settings.DiscountMaxFailedAttempts,
                DiscountLockoutMinutes = settings.DiscountLockoutMinutes,
                DiscountDailyCapPerCustomer = settings.DiscountDailyCapPerCustomer,
                AbandonedCartReminderEnabled = settings.AbandonedCartReminderEnabled,
                AbandonedCartHours = settings.AbandonedCartHours,
                AbandonedCartSmsEnabled = settings.AbandonedCartSmsEnabled,
                AbandonedCartEmailEnabled = settings.AbandonedCartEmailEnabled,
                AbandonedCartDiscountPercent = settings.AbandonedCartDiscountPercent,
                AbandonedCartCodeValidityHours = settings.AbandonedCartCodeValidityHours,
                AbandonedCartSmsTemplate = settings.AbandonedCartSmsTemplate,
                AbandonedCartEmailSubject = settings.AbandonedCartEmailSubject,
                AbandonedCartEmailTemplate = settings.AbandonedCartEmailTemplate
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (settings is null)
        {
            throw new InvalidOperationException("Site settings were not found.");
        }

        return settings with
        {
            IsOnlineOrderingAvailable = PaymentAvailability.IsOnlinePaymentConfigured(_paymentOptions)
        };
    }
}
