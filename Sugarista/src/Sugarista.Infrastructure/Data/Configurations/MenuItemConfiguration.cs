using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sugarista.Domain.Entities;

namespace Sugarista.Infrastructure.Data.Configurations;

public class MenuItemConfiguration : IEntityTypeConfiguration<MenuItem>
{
    public void Configure(EntityTypeBuilder<MenuItem> builder)
    {
        builder.Property(item => item.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(item => item.PersianName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(item => item.EnglishName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(item => item.Description)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(item => item.ImageUrl)
            .HasMaxLength(1000);

        builder.Property(item => item.GalleryImageUrls)
            .HasColumnType("text[]");

        builder.Property(item => item.Badge)
            .HasMaxLength(100);

        builder.Property(item => item.TaxPercent)
            .HasPrecision(5, 2);

        builder.Property(item => item.Volume)
            .HasMaxLength(100);

        builder.Property(item => item.SmallVolume)
            .HasMaxLength(100);

        builder.Property(item => item.MediumVolume)
            .HasMaxLength(100);

        builder.Property(item => item.LargeVolume)
            .HasMaxLength(100);

        builder.Property(item => item.Accent)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(item => item.ShortLabel)
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(item => item.Tags)
            .HasColumnType("text[]");

        builder.Property(item => item.Allergens)
            .HasColumnType("text[]");
    }
}
