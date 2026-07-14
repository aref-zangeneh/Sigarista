using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sugarista.Domain.Entities;

namespace Sugarista.Infrastructure.Data.Configurations;

public class ProductWikiGuideConfiguration : IEntityTypeConfiguration<ProductWikiGuide>
{
    public void Configure(EntityTypeBuilder<ProductWikiGuide> builder)
    {
        builder.Property(g => g.Overview).HasMaxLength(4000);
        builder.Property(g => g.QualityStandards).HasMaxLength(4000);
        builder.Property(g => g.EquipmentNotes).HasMaxLength(2000);
        builder.Property(g => g.ServingNotes).HasMaxLength(2000);

        builder.HasOne(g => g.MenuItem)
            .WithOne()
            .HasForeignKey<ProductWikiGuide>(g => g.MenuItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(g => g.MenuItemId).IsUnique();
    }
}
