using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sugarista.Domain.Entities;

namespace Sugarista.Infrastructure.Data.Configurations;

public class OrderPaymentConfiguration : IEntityTypeConfiguration<OrderPayment>
{
    public void Configure(EntityTypeBuilder<OrderPayment> builder)
    {
        builder.Property(p => p.PayerKey)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(p => p.PayerLabel)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(p => p.Amount)
            .HasColumnType("numeric(18,2)");

        builder.Property(p => p.PaymentAuthority)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(p => p.ReferenceId)
            .HasMaxLength(100);

        builder.Property(p => p.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.HasIndex(p => p.PaymentAuthority);
        builder.HasIndex(p => new { p.OrderId, p.PayerKey });

        builder.HasOne(p => p.Order)
            .WithMany(o => o.Payments)
            .HasForeignKey(p => p.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
