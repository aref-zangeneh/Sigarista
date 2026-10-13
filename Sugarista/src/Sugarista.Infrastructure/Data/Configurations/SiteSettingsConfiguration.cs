using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sugarista.Domain.Entities;

namespace Sugarista.Infrastructure.Data.Configurations;

public class SiteSettingsConfiguration : IEntityTypeConfiguration<SiteSettings>
{
    public void Configure(EntityTypeBuilder<SiteSettings> builder)
    {
        builder.Property(settings => settings.CafeName).HasMaxLength(150).IsRequired();
        builder.Property(settings => settings.BrowserTitle).HasMaxLength(200).IsRequired();
        builder.Property(settings => settings.MetaDescription).HasMaxLength(500).IsRequired();
        builder.Property(settings => settings.Eyebrow).HasMaxLength(150).IsRequired();
        builder.Property(settings => settings.BrandTitle).HasMaxLength(200).IsRequired();
        builder.Property(settings => settings.BrandSubtitle).HasMaxLength(300).IsRequired();
        builder.Property(settings => settings.HeroBadgeText).HasMaxLength(200).IsRequired();
        builder.Property(settings => settings.HeroTitle).HasMaxLength(300).IsRequired();
        builder.Property(settings => settings.HeroDescription).HasMaxLength(1000).IsRequired();
        builder.Property(settings => settings.HeroPrimaryButtonLabel).HasMaxLength(100).IsRequired();
        builder.Property(settings => settings.HeroSecondaryButtonLabel).HasMaxLength(100).IsRequired();
        builder.Property(settings => settings.HeroHours).HasMaxLength(100).IsRequired();
        builder.Property(settings => settings.HowToOrderLabel).HasMaxLength(100).IsRequired();
        builder.Property(settings => settings.HowToOrderTitle).HasMaxLength(200).IsRequired();
        builder.Property(settings => settings.HowToOrderStatus).HasMaxLength(100).IsRequired();
        builder.Property(settings => settings.StepOneTitle).HasMaxLength(200).IsRequired();
        builder.Property(settings => settings.StepOneDescription).HasMaxLength(500).IsRequired();
        builder.Property(settings => settings.StepTwoTitle).HasMaxLength(200).IsRequired();
        builder.Property(settings => settings.StepTwoDescription).HasMaxLength(500).IsRequired();
        builder.Property(settings => settings.StepThreeTitle).HasMaxLength(200).IsRequired();
        builder.Property(settings => settings.StepThreeDescription).HasMaxLength(500).IsRequired();
        builder.Property(settings => settings.QuickAccessLabel).HasMaxLength(100).IsRequired();
        builder.Property(settings => settings.QuickAccessTitle).HasMaxLength(200).IsRequired();
        builder.Property(settings => settings.QuickAccessDescription).HasMaxLength(500).IsRequired();
        builder.Property(settings => settings.PopularLabel).HasMaxLength(100).IsRequired();
        builder.Property(settings => settings.PopularTitle).HasMaxLength(200).IsRequired();
        builder.Property(settings => settings.PopularDescription).HasMaxLength(500).IsRequired();
        builder.Property(settings => settings.MenuLabel).HasMaxLength(100).IsRequired();
        builder.Property(settings => settings.MenuTitle).HasMaxLength(200).IsRequired();
        builder.Property(settings => settings.MenuToolbarHint).HasMaxLength(300).IsRequired();
        builder.Property(settings => settings.SearchPlaceholder).HasMaxLength(200).IsRequired();
        builder.Property(settings => settings.VisitLabel).HasMaxLength(100).IsRequired();
        builder.Property(settings => settings.VisitTitle).HasMaxLength(200).IsRequired();
        builder.Property(settings => settings.VisitDescription).HasMaxLength(1000).IsRequired();
        builder.Property(settings => settings.ContactPhone).HasMaxLength(50).IsRequired();
        builder.Property(settings => settings.Address).HasMaxLength(500).IsRequired();
        builder.Property(settings => settings.BreakfastHours).HasMaxLength(150).IsRequired();
        builder.Property(settings => settings.TakeawayStatus).HasMaxLength(150).IsRequired();
        builder.Property(settings => settings.InstagramUrl).HasMaxLength(300).IsRequired();
        builder.Property(settings => settings.FooterText).HasMaxLength(200).IsRequired();
        builder.Property(settings => settings.FooterPopularLabel).HasMaxLength(100).IsRequired();
        builder.Property(settings => settings.FooterMenuLabel).HasMaxLength(100).IsRequired();
        builder.Property(settings => settings.FooterCallLabel).HasMaxLength(100).IsRequired();
        builder.Property(settings => settings.IsOnlineSalesEnabled).IsRequired();
        builder.Property(settings => settings.OnlineSalesNotice).HasMaxLength(500).IsRequired();
    }
}
