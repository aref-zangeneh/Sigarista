using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sugarista.Domain.Entities;

namespace Sugarista.Infrastructure.Data.Configurations;

public class OrderSplitShareConfiguration : IEntityTypeConfiguration<OrderSplitShare>
{
    public void Configure(EntityTypeBuilder<OrderSplitShare> builder)
    {
        builder.Property(share => share.PayerKey)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(share => share.PayerLabel)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(share => share.SharePercentage)
            .HasColumnType("numeric(18,2)");

        builder.Property(share => share.ShareAmount)
            .HasColumnType("numeric(18,2)");

        builder.HasIndex(share => new { share.OrderId, share.PayerKey });

        builder.HasOne(share => share.Order)
            .WithMany(order => order.SplitShares)
            .HasForeignKey(share => share.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
