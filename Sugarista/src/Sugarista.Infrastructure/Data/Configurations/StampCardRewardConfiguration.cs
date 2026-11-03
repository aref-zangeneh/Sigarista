using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sugarista.Domain.Entities;

namespace Sugarista.Infrastructure.Data.Configurations;

public class StampCardRewardConfiguration : IEntityTypeConfiguration<StampCardReward>
{
    public void Configure(EntityTypeBuilder<StampCardReward> builder)
    {
        builder.HasKey(reward => reward.Id);

        builder.Property(reward => reward.Code).HasMaxLength(30);
        builder.Property(reward => reward.RewardPercent).HasPrecision(5, 2);

        // One reward per completed card per customer: keeps the payout idempotent.
        builder.HasIndex(reward => new { reward.CustomerId, reward.FullCardNumber }).IsUnique();
        builder.HasIndex(reward => reward.CustomerId);
        builder.HasIndex(reward => reward.Code);

        builder.HasOne(reward => reward.Customer)
            .WithMany()
            .HasForeignKey(reward => reward.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable("StampCardRewards");
    }
}

public class MembershipTierConfiguration : IEntityTypeConfiguration<MembershipTier>
{
    public void Configure(EntityTypeBuilder<MembershipTier> builder)
    {
        builder.HasKey(entry => entry.Id);

        builder.Property(entry => entry.Name).HasMaxLength(100);
        builder.Property(entry => entry.Description).HasMaxLength(500);
        builder.Property(entry => entry.MinTotalPaidSpent).HasPrecision(18, 2);
        builder.Property(entry => entry.DiscountPercent).HasPrecision(5, 2);

        builder.HasIndex(entry => entry.IsEnabled);
        builder.HasIndex(entry => entry.DisplayOrder);

        builder.ToTable("MembershipTiers");
    }
}
