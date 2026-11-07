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

    /// <summary>
    /// Whether customers whose window activity drops below their level's
    /// thresholds are demoted automatically. false = levels only ever go up.
    /// </summary>
    public bool MembershipDowngradeEnabled { get; set; } = true;

    /// <summary>Master switch for the customer wallet and cashback program.</summary>
    public bool WalletEnabled { get; set; } = true;

    /// <summary>Percent of each paid order granted back into the customer wallet (0 disables cashback).</summary>
    public decimal WalletCashbackPercent { get; set; } = 2m;

    /// <summary>Minimum order total for cashback to be granted (0 = no minimum).</summary>
    public decimal WalletCashbackMinOrderAmount { get; set; }

    /// <summary>Maximum cashback (toman) granted per single order (0 = no cap).</summary>
    public decimal WalletCashbackMaxPerOrderAmount { get; set; }

    /// <summary>Days before granted cashback expires out of the wallet (0 = never expires).</summary>
    public int WalletCashbackExpiryDays { get; set; }

    /// <summary>
    /// Maximum share (percent) of a single order that may be paid from the
    /// wallet (100 = the wallet may cover the whole order).
    /// </summary>
    public int WalletPaymentMaxPercent { get; set; } = 100;

    /// <summary>Master switch for the birthday discount program.</summary>
    public bool BirthdayDiscountEnabled { get; set; }

    /// <summary>Whether the birthday benefit is auto-applied at checkout or redeemed through a code.</summary>
    public BirthdayDiscountMode BirthdayDiscountMode { get; set; } = BirthdayDiscountMode.Automatic;

    /// <summary>Birthday discount percent off the order items (0-100).</summary>
    public decimal BirthdayDiscountPercent { get; set; }

    /// <summary>
    /// How many days around the birthday the benefit is valid
    /// (0 = birthday itself only, e.g. 7 = birthday week, 30 = birthday month-ish).
    /// </summary>
    public int BirthdayDiscountWindowDays { get; set; }

    /// <summary>Persian greeting shown to the customer while the birthday benefit is active.</summary>
    public string BirthdayGreetingMessage { get; set; } = "تولدت مبارک! 🎉 هدیهٔ کافه به مناسبت تولدت فعال شد.";

    /// <summary>Whether customers may set or change their own birth date in the digital menu.</summary>
    public bool BirthdayEditableByCustomer { get; set; } = true;

    /// <summary>Master switch for the personal-cup (لیوان شخصی) discount.</summary>
    public bool PersonalCupDiscountEnabled { get; set; }

    /// <summary>Fixed discount (toman) granted when the customer brings a personal cup.</summary>
    public decimal PersonalCupDiscountAmount { get; set; }

    /// <summary>Comma-separated category ids the cup discount applies to (empty = all products).</summary>
    public string? PersonalCupCategoryIds { get; set; }

    /// <summary>Delivery/redemption guidance shown to the customer and the barista.</summary>
    public string PersonalCupDeliveryNote { get; set; } =
        "لیوان شخصی را هنگام تحویل به باریستا نشان دهید.";

    /// <summary>Renewal policy text shown to the customer for monthly subscriptions.</summary>
    public string SubscriptionRenewalPolicy { get; set; } =
        "تمدید اشتراک از طریق پیشخوان کافه یا پنل مشتری انجام می‌شود؛ باقی‌ماندهٔ استفاده‌ها به دورهٔ بعد منتقل نمی‌شود مگر با تایید مدیریت.";

    /// <summary>Cancellation & refund policy text for subscriptions.</summary>
    public string SubscriptionCancelRefundPolicy { get; set; } =
        "لغو اشتراک با درخواست مشتری انجام می‌شود؛ معادل استفاده‌های باقی‌مانده به کیف پول مشتری برگشت می‌خورد.";

    /// <summary>
    /// Master switch for pickup scheduling (سفارش پیش از مراجعه). When false,
    /// customers cannot choose a future pickup time and the server rejects any
    /// DeliveryAt on checkout.
    /// </summary>
    public bool PickupSchedulingEnabled { get; set; }

    /// <summary>Length of one pickup slot in minutes (e.g. 10 or 15).</summary>
    public int PickupSlotMinutes { get; set; } = 15;

    /// <summary>Maximum load allowed per pickup slot.</summary>
    public int PickupSlotCapacity { get; set; } = 5;

    /// <summary>
    /// What the capacity counts: "Orders" (number of orders) or "Items"
    /// (sum of line quantities). Any other value is treated as "Orders".
    /// </summary>
    public string PickupCapacityMode { get; set; } = "Orders";

    /// <summary>
    /// Minimum minutes between the moment of ordering and the chosen pickup
    /// time. Keeps the kitchen from being asked for an impossible slot.
    /// </summary>
    public int PickupMinLeadMinutes { get; set; } = 30;

    /// <summary>How many days into the future a pickup order may be placed (0 = today only).</summary>
    public int PickupMaxDaysAhead { get; set; } = 7;

    /// <summary>
    /// Minutes before the pickup time during which the customer may still
    /// cancel or reschedule. After this window both actions are blocked.
    /// </summary>
    public int PickupCancelCutoffMinutes { get; set; } = 60;

    /// <summary>
    /// How a paid pickup order is refunded when the customer cancels inside
    /// the allowed window:
    /// "Wallet"     → automatic refund to the customer wallet,
    /// "CardToCard" → the café settles the refund manually (card-to-card).
    /// Any other value is treated as "Wallet".
    /// </summary>
    public string PickupRefundMode { get; set; } = "Wallet";

    /// <summary>
    /// Maximum number of times a customer may reschedule the same pickup
    /// order. 0 = unlimited. The counter lives on <see cref="Order"/>.
    /// </summary>
    public int PickupMaxReschedules { get; set; } = 2;

    /// <summary>Customer-facing explanation of the refund policy for pickup orders.</summary>
    public string PickupRefundPolicyNote { get; set; } =
        "در صورت لغو سفارش پرداختشده تا مهلت مجاز، مبلغ طبق سیاست کافه به کیف پول شما برگشت داده میشود.";

    /// <summary>Master switch for the custom drink builder in the digital menu.</summary>
    public bool CustomDrinkBuilderEnabled { get; set; } = true;

    /// <summary>Master switch for discount abuse protection (lockout, blacklist, daily cap).</summary>
    public bool DiscountAbuseProtectionEnabled { get; set; } = true;

    /// <summary>Failed code attempts allowed inside the lockout window before a temporary lock.</summary>
    public int DiscountMaxFailedAttempts { get; set; } = 5;

    /// <summary>How long a phone/identity stays locked after too many failed attempts (minutes).</summary>
    public int DiscountLockoutMinutes { get; set; } = 15;

    /// <summary>
    /// Maximum total discount (toman) a single customer may receive per day
    /// across all discounts. 0 = unlimited.
    /// </summary>
    public decimal DiscountDailyCapPerCustomer { get; set; }

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

    /// <summary>
    /// Visual layout of the printed receipts (customer copy + barista/kitchen
    /// ticket) as a serialized JSON document
    /// (<see cref="Sugarista.Application.Printing.ReceiptLayout.ResolvedReceiptLayout"/>).
    /// null = the built-in default layout. Configured from the admin panel,
    /// applied by <c>ReceiptPrintJobService</c> and the offline print agent.
    /// </summary>
    public string? ReceiptLayoutJson { get; set; }

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
