using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sugarista.Domain.Entities;

namespace Sugarista.Infrastructure.Data.Configurations;

public class MenuItemRecipeCardConfiguration : IEntityTypeConfiguration<MenuItemRecipeCard>
{
    public void Configure(EntityTypeBuilder<MenuItemRecipeCard> builder)
    {
        builder.ToTable("MenuItemRecipeCards");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.CardImageUrl)
            .HasMaxLength(500);

        builder.Property(x => x.BulletPointsJson)
            .HasMaxLength(4000)
            .HasDefaultValue("[]")
            .IsRequired();

        builder.Property(x => x.ServingTemp)
            .HasMaxLength(40);

        builder.Property(x => x.GlasswareOrPlate)
            .HasMaxLength(80);

        builder.Property(x => x.BaristaTip)
            .HasMaxLength(400);

        builder.HasOne(x => x.MenuItem)
            .WithMany()
            .HasForeignKey(x => x.MenuItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.MenuItemId).IsUnique();
    }
}
