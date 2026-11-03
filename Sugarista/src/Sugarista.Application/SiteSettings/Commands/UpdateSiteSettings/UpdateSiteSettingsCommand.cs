using Microsoft.EntityFrameworkCore;

namespace Sugarista.Application.SiteSettings.Commands.UpdateSiteSettings;

public record UpdateSiteSettingsCommand : IRequest<Guid>
{
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

    public int WinBackDays { get; init; } = 30;

    public bool LoyaltyEnabled { get; init; }

    public decimal LoyaltyEarnTomanPerPoint { get; init; } = 10000m;

    public decimal LoyaltyRedeemTomanPerPoint { get; init; } = 1000m;

    public int LoyaltyPointsExpiryDays { get; init; } = 365;

    public bool StampCardEnabled { get; init; } = true;
    public int StampCardOrdersPerCard { get; init; } = 10;
    public decimal StampCardRewardPercent { get; init; } = 10m;
    public int StampCardRewardValidityDays { get; init; } = 30;

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

public class UpdateSiteSettingsCommandValidator : AbstractValidator<UpdateSiteSettingsCommand>
{
    public UpdateSiteSettingsCommandValidator()
    {
        RuleFor(command => command.CafeName).NotEmpty().MaximumLength(150);
        RuleFor(command => command.BrowserTitle).NotEmpty().MaximumLength(200);
        RuleFor(command => command.MetaDescription).NotEmpty().MaximumLength(500);
        RuleFor(command => command.ContactPhone).MaximumLength(50);
        RuleFor(command => command.Address).MaximumLength(500);
        RuleFor(command => command.InstagramUrl).MaximumLength(300);
        RuleFor(command => command.TimeZoneId).NotEmpty().MaximumLength(64);
        RuleFor(command => command.LightThemeStartHour).InclusiveBetween(0, 23);
        RuleFor(command => command.DarkThemeStartHour).InclusiveBetween(0, 23);
        RuleFor(command => command.ThemeMode)
            .Must(mode => mode is "auto" or "light" or "dark")
            .WithMessage("حالت تم باید یکی از auto، light یا dark باشد.");
        RuleFor(command => command.WinBackDays).InclusiveBetween(0, 365);
        RuleFor(command => command.LoyaltyEarnTomanPerPoint).GreaterThanOrEqualTo(0);
        RuleFor(command => command.LoyaltyRedeemTomanPerPoint).GreaterThanOrEqualTo(0);
        RuleFor(command => command.LoyaltyPointsExpiryDays).InclusiveBetween(0, 3650);
        RuleFor(command => command.StampCardOrdersPerCard).InclusiveBetween(1, 1000);
        RuleFor(command => command.StampCardRewardPercent).InclusiveBetween(0, 100);
        RuleFor(command => command.StampCardRewardValidityDays).InclusiveBetween(0, 3650);
        RuleFor(command => command.AbandonedCartHours).InclusiveBetween(0, 720);
        RuleFor(command => command.AbandonedCartDiscountPercent).InclusiveBetween(0, 100);
        RuleFor(command => command.AbandonedCartCodeValidityHours).InclusiveBetween(1, 720);
        RuleFor(command => command.AbandonedCartSmsTemplate).MaximumLength(1000);
        RuleFor(command => command.AbandonedCartEmailSubject).MaximumLength(200);
        RuleFor(command => command.AbandonedCartEmailTemplate).MaximumLength(4000);
    }
}

public class UpdateSiteSettingsCommandHandler : IRequestHandler<UpdateSiteSettingsCommand, Guid>
{
    private readonly IApplicationDbContext _dbContext;

    public UpdateSiteSettingsCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Guid> Handle(UpdateSiteSettingsCommand request, CancellationToken cancellationToken)
    {
        var settings = await _dbContext.SiteSettings.FirstOrDefaultAsync(cancellationToken);

        if (settings is null)
        {
            settings = new Domain.Entities.SiteSettings();
            _dbContext.SiteSettings.Add(settings);
        }

        settings.CafeName = request.CafeName.Trim();
        settings.BrowserTitle = request.BrowserTitle.Trim();
        settings.MetaDescription = request.MetaDescription.Trim();
        settings.Eyebrow = request.Eyebrow.Trim();
        settings.BrandTitle = request.BrandTitle.Trim();
        settings.BrandSubtitle = request.BrandSubtitle.Trim();
        settings.HeroBadgeText = request.HeroBadgeText.Trim();
        settings.HeroTitle = request.HeroTitle.Trim();
        settings.HeroDescription = request.HeroDescription.Trim();
        settings.HeroPrimaryButtonLabel = request.HeroPrimaryButtonLabel.Trim();
        settings.HeroSecondaryButtonLabel = request.HeroSecondaryButtonLabel.Trim();
        settings.HeroHours = request.HeroHours.Trim();
        settings.HowToOrderLabel = request.HowToOrderLabel.Trim();
        settings.HowToOrderTitle = request.HowToOrderTitle.Trim();
        settings.HowToOrderStatus = request.HowToOrderStatus.Trim();
        settings.StepOneTitle = request.StepOneTitle.Trim();
        settings.StepOneDescription = request.StepOneDescription.Trim();
        settings.StepTwoTitle = request.StepTwoTitle.Trim();
        settings.StepTwoDescription = request.StepTwoDescription.Trim();
        settings.StepThreeTitle = request.StepThreeTitle.Trim();
        settings.StepThreeDescription = request.StepThreeDescription.Trim();
        settings.QuickAccessLabel = request.QuickAccessLabel.Trim();
        settings.QuickAccessTitle = request.QuickAccessTitle.Trim();
        settings.QuickAccessDescription = request.QuickAccessDescription.Trim();
        settings.PopularLabel = request.PopularLabel.Trim();
        settings.PopularTitle = request.PopularTitle.Trim();
        settings.PopularDescription = request.PopularDescription.Trim();
        settings.MenuLabel = request.MenuLabel.Trim();
        settings.MenuTitle = request.MenuTitle.Trim();
        settings.MenuToolbarHint = request.MenuToolbarHint.Trim();
        settings.SearchPlaceholder = request.SearchPlaceholder.Trim();
        settings.VisitLabel = request.VisitLabel.Trim();
        settings.VisitTitle = request.VisitTitle.Trim();
        settings.VisitDescription = request.VisitDescription.Trim();
        settings.ContactPhone = request.ContactPhone.Trim();
        settings.Address = request.Address.Trim();
        settings.BreakfastHours = request.BreakfastHours.Trim();
        settings.TakeawayStatus = request.TakeawayStatus.Trim();
        settings.InstagramUrl = request.InstagramUrl.Trim();
        settings.FooterText = request.FooterText.Trim();
        settings.FooterPopularLabel = request.FooterPopularLabel.Trim();
        settings.FooterMenuLabel = request.FooterMenuLabel.Trim();
        settings.FooterCallLabel = request.FooterCallLabel.Trim();
        settings.IsOnlineSalesEnabled = request.IsOnlineSalesEnabled;
        settings.OnlineSalesNotice = request.OnlineSalesNotice.Trim();
        settings.TimeZoneId = request.TimeZoneId.Trim();
        settings.LightThemeStartHour = request.LightThemeStartHour;
        settings.DarkThemeStartHour = request.DarkThemeStartHour;
        settings.ThemeMode = request.ThemeMode.Trim().ToLowerInvariant();
        settings.AllowManualThemeOverride = request.AllowManualThemeOverride;
        settings.WinBackDays = request.WinBackDays;
        settings.LoyaltyEnabled = request.LoyaltyEnabled;
        settings.LoyaltyEarnTomanPerPoint = request.LoyaltyEarnTomanPerPoint;
        settings.LoyaltyRedeemTomanPerPoint = request.LoyaltyRedeemTomanPerPoint;
        settings.LoyaltyPointsExpiryDays = request.LoyaltyPointsExpiryDays;
        settings.StampCardEnabled = request.StampCardEnabled;
        settings.StampCardOrdersPerCard = request.StampCardOrdersPerCard;
        settings.StampCardRewardPercent = request.StampCardRewardPercent;
        settings.StampCardRewardValidityDays = request.StampCardRewardValidityDays;
        settings.AbandonedCartReminderEnabled = request.AbandonedCartReminderEnabled;
        settings.AbandonedCartHours = request.AbandonedCartHours;
        settings.AbandonedCartSmsEnabled = request.AbandonedCartSmsEnabled;
        settings.AbandonedCartEmailEnabled = request.AbandonedCartEmailEnabled;
        settings.AbandonedCartDiscountPercent = request.AbandonedCartDiscountPercent;
        settings.AbandonedCartCodeValidityHours = request.AbandonedCartCodeValidityHours;
        settings.AbandonedCartSmsTemplate = request.AbandonedCartSmsTemplate.Trim();
        settings.AbandonedCartEmailSubject = request.AbandonedCartEmailSubject.Trim();
        settings.AbandonedCartEmailTemplate = request.AbandonedCartEmailTemplate.Trim();

        await _dbContext.SaveChangesAsync(cancellationToken);

        return settings.Id;
    }
}
