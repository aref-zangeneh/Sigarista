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
