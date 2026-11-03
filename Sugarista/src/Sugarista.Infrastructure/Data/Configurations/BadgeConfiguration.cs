using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sugarista.Domain.Entities;

namespace Sugarista.Infrastructure.Data.Configurations;

public class BadgeConfiguration : IEntityTypeConfiguration<Badge>
{
    public void Configure(EntityTypeBuilder<Badge> builder)
    {
        builder.HasKey(entry => entry.Id);

        builder.Property(entry => entry.Name).HasMaxLength(100);
        builder.Property(entry => entry.Description).HasMaxLength(500);
        builder.Property(entry => entry.ImageUrl).HasMaxLength(500);
        builder.Property(entry => entry.MinTotalSpent).HasPrecision(18, 2);

        builder.HasIndex(entry => entry.IsEnabled);

        builder.HasMany(entry => entry.Awards)
            .WithOne(award => award.Badge)
            .HasForeignKey(award => award.BadgeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable("Badges");
    }
}

public class CustomerBadgeConfiguration : IEntityTypeConfiguration<CustomerBadge>
{
    public void Configure(EntityTypeBuilder<CustomerBadge> builder)
    {
        builder.HasKey(entry => entry.Id);

        // One award per badge per customer keeps the automatic grant idempotent.
        builder.HasIndex(entry => new { entry.CustomerId, entry.BadgeId }).IsUnique();
        builder.HasIndex(entry => entry.BadgeId);

        builder.HasOne(entry => entry.Customer)
            .WithMany()
            .HasForeignKey(entry => entry.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable("CustomerBadges");
    }
}
