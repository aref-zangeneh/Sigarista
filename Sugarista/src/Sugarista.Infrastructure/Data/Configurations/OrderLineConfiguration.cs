using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sugarista.Domain.Entities;

namespace Sugarista.Infrastructure.Data.Configurations;

public class OrderLineConfiguration : IEntityTypeConfiguration<OrderLine>
{
    public void Configure(EntityTypeBuilder<OrderLine> builder)
    {
        builder.Property(l => l.PersianName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(l => l.EnglishName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(l => l.SizeKey)
            .HasMaxLength(20);

        builder.Property(l => l.SizeLabel)
            .HasMaxLength(50);

        builder.Property(l => l.ModifierLabels)
            .HasColumnType("text[]");

        builder.Property(l => l.PayerKey)
            .HasMaxLength(50);

        builder.Property(l => l.PayerLabel)
            .HasMaxLength(100);

        builder.Property(l => l.BaristaNote)
            .HasMaxLength(500);

        builder.Property(l => l.UnitPrice)
            .HasColumnType("numeric(18,2)");

        builder.Property(l => l.LineTotal)
            .HasColumnType("numeric(18,2)");

        builder.HasOne(l => l.Order)
            .WithMany(o => o.Lines)
            .HasForeignKey(l => l.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(l => l.MenuItem)
            .WithMany()
            .HasForeignKey(l => l.MenuItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
