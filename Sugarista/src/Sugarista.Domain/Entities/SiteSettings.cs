using Sugarista.Domain.Common;

namespace Sugarista.Domain.Entities;

public class SiteSettings : BaseAuditableEntity
{
    public string CafeName { get; set; } = string.Empty;

    public string BrowserTitle { get; set; } = string.Empty;

    public string MetaDescription { get; set; } = string.Empty;

    public string Eyebrow { get; set; } = string.Empty;

    public string BrandTitle { get; set; } = string.Empty;

    public string BrandSubtitle { get; set; } = string.Empty;

    public string HeroBadgeText { get; set; } = string.Empty;

    public string HeroTitle { get; set; } = string.Empty;

    public string HeroDescription { get; set; } = string.Empty;

    public string HeroPrimaryButtonLabel { get; set; } = string.Empty;

    public string HeroSecondaryButtonLabel { get; set; } = string.Empty;

    public string HeroHours { get; set; } = string.Empty;

    public string HowToOrderLabel { get; set; } = string.Empty;

    public string HowToOrderTitle { get; set; } = string.Empty;

    public string HowToOrderStatus { get; set; } = string.Empty;

    public string StepOneTitle { get; set; } = string.Empty;

    public string StepOneDescription { get; set; } = string.Empty;

    public string StepTwoTitle { get; set; } = string.Empty;

    public string StepTwoDescription { get; set; } = string.Empty;

    public string StepThreeTitle { get; set; } = string.Empty;

    public string StepThreeDescription { get; set; } = string.Empty;

    public string QuickAccessLabel { get; set; } = string.Empty;

    public string QuickAccessTitle { get; set; } = string.Empty;

    public string QuickAccessDescription { get; set; } = string.Empty;

    public string PopularLabel { get; set; } = string.Empty;

    public string PopularTitle { get; set; } = string.Empty;

    public string PopularDescription { get; set; } = string.Empty;

    public string MenuLabel { get; set; } = string.Empty;

    public string MenuTitle { get; set; } = string.Empty;

    public string MenuToolbarHint { get; set; } = string.Empty;

    public string SearchPlaceholder { get; set; } = string.Empty;

    public string VisitLabel { get; set; } = string.Empty;

    public string VisitTitle { get; set; } = string.Empty;

    public string VisitDescription { get; set; } = string.Empty;

    public string ContactPhone { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    public string BreakfastHours { get; set; } = string.Empty;

    public string TakeawayStatus { get; set; } = string.Empty;

    public string InstagramUrl { get; set; } = string.Empty;

    public string FooterText { get; set; } = string.Empty;

    public string FooterPopularLabel { get; set; } = string.Empty;

    public string FooterMenuLabel { get; set; } = string.Empty;

    public string FooterCallLabel { get; set; } = string.Empty;

    public bool IsOnlineSalesEnabled { get; set; } = true;

    public string OnlineSalesNotice { get; set; } = string.Empty;

    /// <summary>
    /// IANA time zone id of the café (e.g. "Asia/Tehran"). Drives the
    /// time-of-day theme and any other local-time logic on the public menu.
    /// </summary>
    public string TimeZoneId { get; set; } = "Asia/Tehran";

    /// <summary>
    /// Hour (0-23, café local time) at which the light theme begins. Used when
    /// <see cref="ThemeMode"/> is "auto".
    /// </summary>
    public int LightThemeStartHour { get; set; } = 6;

    /// <summary>
    /// Hour (0-23, café local time) at which the dark theme begins. Used when
    /// <see cref="ThemeMode"/> is "auto". May be smaller than
    /// <see cref="LightThemeStartHour"/> (dark theme then spans midnight).
    /// </summary>
    public int DarkThemeStartHour { get; set; } = 20;

    /// <summary>
    /// "auto" (follow the schedule), "light" or "dark" (force one theme on the
    /// public menu regardless of the time of day).
    /// </summary>
    public string ThemeMode { get; set; } = "auto";

    /// <summary>
    /// Whether visitors may override the theme manually (toggle on the menu).
    /// When false, the toggle is hidden and the schedule/mode always wins.
    /// </summary>
    public bool AllowManualThemeOverride { get; set; } = true;

    /// <summary>Days without a paid order before the win-back SMS dispatch sends a comeback discount code (0 disables the sweep).</summary>
    public int WinBackDays { get; set; } = 30;

    /// <summary>Master switch for the loyalty points program.</summary>
    public bool LoyaltyEnabled { get; set; }

    /// <summary>How many toman of spending earn one loyalty point (0 disables earning).</summary>
    public decimal LoyaltyEarnTomanPerPoint { get; set; } = 10000m;

    /// <summary>How many toman one loyalty point is worth when redeemed (0 disables redemption).</summary>
    public decimal LoyaltyRedeemTomanPerPoint { get; set; } = 1000m;

    /// <summary>Days before earned points expire (0 = never).</summary>
    public int LoyaltyPointsExpiryDays { get; set; } = 365;

    /// <summary>Master switch for the stamp card loyalty game.</summary>
    public bool StampCardEnabled { get; set; } = true;

    /// <summary>Paid orders needed to complete one stamp card.</summary>
    public int StampCardOrdersPerCard { get; set; } = 10;

    /// <summary>Percentage discount granted when a stamp card is completed.</summary>
    public decimal StampCardRewardPercent { get; set; } = 10m;

    /// <summary>Days the completed-card reward stays valid (0 = no expiry).</summary>
    public int StampCardRewardValidityDays { get; set; } = 30;

    /// <summary>Master switch for membership level auto-upgrades.</summary>
    public bool MembershipTiersEnabled { get; set; } = true;

    /// <summary>Rolling window (days) for evaluating membership level spending (0 = lifetime).</summary>
    public int MembershipWindowDays { get; set; } = 365;

    /// <summary>Whether the abandoned-cart reminder sweep runs at all.</summary>
    public bool AbandonedCartReminderEnabled { get; set; } = true;

    /// <summary>
    /// Hours of inactivity (without a paid order) before the abandoned-cart
    /// reminder is sent. 0 disables the sweep.
    /// </summary>
    public int AbandonedCartHours { get; set; } = 4;

    /// <summary>Deliver the abandoned-cart reminder over SMS.</summary>
    public bool AbandonedCartSmsEnabled { get; set; } = true;

    /// <summary>Deliver the abandoned-cart reminder over email (when an email is known).</summary>
    public bool AbandonedCartEmailEnabled { get; set; }

    /// <summary>Percentage off granted by the single-use abandoned-cart code.</summary>
    public decimal AbandonedCartDiscountPercent { get; set; } = 10m;

    /// <summary>How long the single-use abandoned-cart code stays valid, in hours.</summary>
    public int AbandonedCartCodeValidityHours { get; set; } = 48;

    /// <summary>
    /// SMS text template. Placeholders: {name}, {cafe}, {code}, {percent},
    /// {validityHours}, {itemCount}, {itemsTotal}, {items}.
    /// </summary>
    public string AbandonedCartSmsTemplate { get; set; } =
        "سلام {name}، سبد خرید شما در {cafe} منتظر شماست. با کد «{code}» تا {validityHours} ساعت آینده {percent}٪ تخفیف بگیرید.";

    /// <summary>Email subject template. Same placeholders as the SMS template.</summary>
    public string AbandonedCartEmailSubject { get; set; } = "سبد خرید شما در {cafe} منتظر است";

    /// <summary>
    /// Email body template (plain text; line breaks become HTML). Same
    /// placeholders as the SMS template.
    /// </summary>
    public string AbandonedCartEmailTemplate { get; set; } =
        "سلام {name}،\nسبد خرید شما در {cafe} ذخیره شده است:\n{items}\n\nکد تخفیف یک‌بارمصرف شما: {code}\nمیزان تخفیف: {percent}٪\nاعتبار کد: {validityHours} ساعت\n\nمنتظر حضور شما هستیم.";
}
