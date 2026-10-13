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
}

public class UpdateSiteSettingsCommandValidator : AbstractValidator<UpdateSiteSettingsCommand>
{
    public UpdateSiteSettingsCommandValidator()
    {
        RuleFor(command => command.CafeName).NotEmpty().MaximumLength(150);
        RuleFor(command => command.BrowserTitle).NotEmpty().MaximumLength(200);
        RuleFor(command => command.MetaDescription).NotEmpty().MaximumLength(500);
        RuleFor(command => command.ContactPhone).NotEmpty().MaximumLength(50);
        RuleFor(command => command.Address).NotEmpty().MaximumLength(500);
        RuleFor(command => command.InstagramUrl).NotEmpty().MaximumLength(300);
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

        await _dbContext.SaveChangesAsync(cancellationToken);

        return settings.Id;
    }
}
