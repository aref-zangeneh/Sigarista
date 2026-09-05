using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sugarista.Domain.Entities;

namespace Sugarista.Infrastructure.Data.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.Property(o => o.TrackingCode)
            .HasMaxLength(20)
            .IsRequired();

        builder.HasIndex(o => o.TrackingCode).IsUnique();

        builder.Property(o => o.CustomerName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(o => o.CustomerPhone)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(o => o.Note)
            .HasMaxLength(500);

        builder.Property(o => o.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(o => o.FulfillmentStatus)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(o => o.TotalAmount)
            .HasColumnType("numeric(18,2)");

        builder.Property(o => o.AmountPaid)
            .HasColumnType("numeric(18,2)");

        builder.Property(o => o.SplitBillMode)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(o => o.PaymentAuthority)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(o => o.Channel)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(o => o.ServiceType)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(o => o.PaymentMethod)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.HasOne(o => o.SalonTable)
            .WithMany()
            .HasForeignKey(o => o.SalonTableId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(o => o.TableSession)
            .WithMany()
            .HasForeignKey(o => o.TableSessionId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(o => o.Channel);
        builder.HasIndex(o => o.SalonTableId);
        builder.HasIndex(o => o.TableSessionId);
        builder.HasIndex(o => o.Status);

        builder.Property(o => o.IsSplitBill)
            .IsRequired();

        builder.HasIndex(o => o.IsSplitBill);

        builder.HasIndex(o => o.PaymentAuthority);

        builder.HasIndex(o => o.CreatedAt);

        builder.HasIndex(o => o.FulfillmentStatus);
    }
}
