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
    public bool IsOnlineOrderingAvailable { get; init; } = true;
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
                OnlineSalesNotice = settings.OnlineSalesNotice
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
