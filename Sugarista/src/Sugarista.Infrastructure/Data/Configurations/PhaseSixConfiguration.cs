using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sugarista.Domain.Entities;

namespace Sugarista.Infrastructure.Data.Configurations;

public class OrderDiscountConfiguration : IEntityTypeConfiguration<OrderDiscount>
{
    public void Configure(EntityTypeBuilder<OrderDiscount> builder)
    {
        builder.HasKey(discount => discount.Id);

        builder.Property(discount => discount.Kind)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(discount => discount.Name).HasMaxLength(200).IsRequired();
        builder.Property(discount => discount.Code).HasMaxLength(64);
        builder.Property(discount => discount.Amount).HasPrecision(18, 2);

        builder.HasOne(discount => discount.Order)
            .WithMany(order => order.Discounts)
            .HasForeignKey(discount => discount.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(discount => discount.Customer)
            .WithMany()
            .HasForeignKey(discount => discount.CustomerId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(discount => discount.Discount)
            .WithMany()
            .HasForeignKey(discount => discount.DiscountId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(discount => new { discount.CustomerId, discount.RedeemedAt });
        builder.HasIndex(discount => new { discount.OrderId, discount.Kind });
        builder.HasIndex(discount => discount.RedeemedAt);
    }
}

public class DiscountAttemptConfiguration : IEntityTypeConfiguration<DiscountAttempt>
{
    public void Configure(EntityTypeBuilder<DiscountAttempt> builder)
    {
        builder.HasKey(attempt => attempt.Id);

        builder.Property(attempt => attempt.Identifier).HasMaxLength(64).IsRequired();
        builder.Property(attempt => attempt.Code).HasMaxLength(64);
        builder.Property(attempt => attempt.IpAddress).HasMaxLength(64);

        builder.HasIndex(attempt => new { attempt.Identifier, attempt.AttemptedAt });
        builder.HasIndex(attempt => attempt.AttemptedAt);
    }
}

public class BlacklistEntryConfiguration : IEntityTypeConfiguration<BlacklistEntry>
{
    public void Configure(EntityTypeBuilder<BlacklistEntry> builder)
    {
        builder.HasKey(entry => entry.Id);

        builder.Property(entry => entry.Kind)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(entry => entry.Value).HasMaxLength(64).IsRequired();
        builder.Property(entry => entry.Reason).HasMaxLength(500);

        // One row per value; reactivating flips the flag instead of duplicating.
        builder.HasIndex(entry => entry.Value).IsUnique();
        builder.HasIndex(entry => new { entry.Kind, entry.IsActive });
    }
}

public class ReportScheduleConfiguration : IEntityTypeConfiguration<ReportSchedule>
{
    public void Configure(EntityTypeBuilder<ReportSchedule> builder)
    {
        builder.HasKey(schedule => schedule.Id);

        builder.Property(schedule => schedule.Name).HasMaxLength(150).IsRequired();
        builder.Property(schedule => schedule.Kind).HasMaxLength(60).IsRequired();

        builder.Property(schedule => schedule.Frequency)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(schedule => schedule.RecipientEmails).HasMaxLength(1000).IsRequired();
        builder.Property(schedule => schedule.OptionsJson).HasColumnType("text");

        builder.HasIndex(schedule => new { schedule.IsEnabled, schedule.Frequency });
    }
}
