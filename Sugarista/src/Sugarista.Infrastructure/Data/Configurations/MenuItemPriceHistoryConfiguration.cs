using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sugarista.Domain.Entities;

namespace Sugarista.Infrastructure.Data.Configurations;

public class MenuItemPriceHistoryConfiguration : IEntityTypeConfiguration<MenuItemPriceHistory>
{
    public void Configure(EntityTypeBuilder<MenuItemPriceHistory> builder)
    {
        builder.Property(h => h.PreviousPurchasePrice).HasColumnType("numeric(18,2)");
        builder.Property(h => h.NewPurchasePrice).HasColumnType("numeric(18,2)");
        builder.Property(h => h.PurchasePriceDelta).HasColumnType("numeric(18,2)");
        builder.Property(h => h.PurchasePriceChangePercent).HasPrecision(8, 2);

        builder.Property(h => h.PreviousSalePrice).HasColumnType("numeric(18,2)");
        builder.Property(h => h.NewSalePrice).HasColumnType("numeric(18,2)");
        builder.Property(h => h.SalePriceDelta).HasColumnType("numeric(18,2)");
        builder.Property(h => h.SalePriceChangePercent).HasPrecision(8, 2);

        builder.Property(h => h.Source)
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(h => h.Note)
            .HasMaxLength(500);

        builder.Property(h => h.ChangedBy)
            .HasMaxLength(150);

        builder.HasIndex(h => new { h.MenuItemId, h.RecordedAt });

        builder.HasOne(h => h.MenuItem)
            .WithMany()
            .HasForeignKey(h => h.MenuItemId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
