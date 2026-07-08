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
}
