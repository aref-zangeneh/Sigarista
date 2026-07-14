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

        builder.Property(o => o.IsSplitBill)
            .IsRequired();

        builder.HasIndex(o => o.IsSplitBill);

        builder.HasIndex(o => o.PaymentAuthority);

        builder.HasIndex(o => o.CreatedAt);
    }
}
