using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sugarista.Domain.Entities;

namespace Sugarista.Infrastructure.Data.Configurations;

public class DiscountConfiguration : IEntityTypeConfiguration<Discount>
{
    public void Configure(EntityTypeBuilder<Discount> builder)
    {
        builder.HasKey(discount => discount.Id);

        builder.Property(discount => discount.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(discount => discount.Description)
            .HasMaxLength(1000);

        builder.Property(discount => discount.Code)
            .HasMaxLength(64);

        // Codes are unique when present; automatic discounts have no code.
        builder.HasIndex(discount => discount.Code)
            .IsUnique()
            .HasFilter("\"Code\" IS NOT NULL");

        builder.Property(discount => discount.Value).HasPrecision(18, 2);
        builder.Property(discount => discount.MaxDiscountAmount).HasPrecision(18, 2);
        builder.Property(discount => discount.MinCartAmount).HasPrecision(18, 2);

        builder.HasIndex(discount => new { discount.IsActive, discount.Priority });

        builder.HasMany(discount => discount.MenuItems)
            .WithOne(item => item.Discount)
            .HasForeignKey(item => item.DiscountId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(discount => discount.Categories)
            .WithOne(item => item.Discount)
            .HasForeignKey(item => item.DiscountId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(discount => discount.Schedules)
            .WithOne(schedule => schedule.Discount)
            .HasForeignKey(schedule => schedule.DiscountId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(discount => discount.Tiers)
            .WithOne(tier => tier.Discount)
            .HasForeignKey(tier => tier.DiscountId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable("Discounts");
    }
}

public class DiscountTierConfiguration : IEntityTypeConfiguration<DiscountTier>
{
    public void Configure(EntityTypeBuilder<DiscountTier> builder)
    {
        builder.HasKey(tier => tier.Id);

        builder.Property(tier => tier.ThresholdAmount).HasPrecision(18, 2);
        builder.Property(tier => tier.Value).HasPrecision(18, 2);

        // One tier per threshold keeps the ladder unambiguous.
        builder.HasIndex(tier => new { tier.DiscountId, tier.ThresholdAmount }).IsUnique();

        builder.ToTable("DiscountTiers");
    }
}

public class DiscountMenuItemConfiguration : IEntityTypeConfiguration<DiscountMenuItem>
{
    public void Configure(EntityTypeBuilder<DiscountMenuItem> builder)
    {
        builder.HasKey(item => item.Id);

        builder.HasIndex(item => new { item.DiscountId, item.MenuItemId }).IsUnique();

        builder.HasOne(item => item.MenuItem)
            .WithMany()
            .HasForeignKey(item => item.MenuItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable("DiscountMenuItems");
    }
}

public class DiscountCategoryConfiguration : IEntityTypeConfiguration<DiscountCategory>
{
    public void Configure(EntityTypeBuilder<DiscountCategory> builder)
    {
        builder.HasKey(item => item.Id);

        builder.HasIndex(item => new { item.DiscountId, item.CategoryId }).IsUnique();

        builder.HasOne(item => item.Category)
            .WithMany()
            .HasForeignKey(item => item.CategoryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable("DiscountCategories");
    }
}

public class DiscountScheduleConfiguration : IEntityTypeConfiguration<DiscountSchedule>
{
    public void Configure(EntityTypeBuilder<DiscountSchedule> builder)
    {
        builder.HasKey(schedule => schedule.Id);

        builder.HasIndex(schedule => schedule.DiscountId);

        builder.ToTable("DiscountSchedules");
    }
}

public class DiscountRedemptionConfiguration : IEntityTypeConfiguration<DiscountRedemption>
{
    public void Configure(EntityTypeBuilder<DiscountRedemption> builder)
    {
        builder.HasKey(redemption => redemption.Id);

        builder.Property(redemption => redemption.Code).HasMaxLength(64);
        builder.Property(redemption => redemption.Amount).HasPrecision(18, 2);

        // One redemption per discount per order keeps retries idempotent.
        builder.HasIndex(redemption => new { redemption.DiscountId, redemption.OrderId }).IsUnique();
        builder.HasIndex(redemption => redemption.CustomerId);
        builder.HasIndex(redemption => redemption.RedeemedAt);

        builder.HasOne(redemption => redemption.Discount)
            .WithMany()
            .HasForeignKey(redemption => redemption.DiscountId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(redemption => redemption.Customer)
            .WithMany()
            .HasForeignKey(redemption => redemption.CustomerId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.ToTable("DiscountRedemptions");
    }
}
