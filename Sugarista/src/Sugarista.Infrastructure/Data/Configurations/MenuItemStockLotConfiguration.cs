using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sugarista.Domain.Entities;

namespace Sugarista.Infrastructure.Data.Configurations;

public class MenuItemStockLotConfiguration : IEntityTypeConfiguration<MenuItemStockLot>
{
    public void Configure(EntityTypeBuilder<MenuItemStockLot> builder)
    {
        builder.Property(l => l.InitialQuantity).HasPrecision(18, 3).IsRequired();
        builder.Property(l => l.RemainingQuantity).HasPrecision(18, 3).IsRequired();
        builder.Property(l => l.ReceivedAt).IsRequired();
        builder.Property(l => l.BatchLabel).HasMaxLength(120);
        builder.Property(l => l.IsDiscarded).HasDefaultValue(false).IsRequired();
        builder.Property(l => l.DiscardReason).HasMaxLength(200);

        builder.HasOne(l => l.MenuItem)
            .WithMany()
            .HasForeignKey(l => l.MenuItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(l => l.MenuItemId);
        builder.HasIndex(l => l.ExpiresAt);
        builder.HasIndex(l => new { l.MenuItemId, l.IsDiscarded, l.RemainingQuantity, l.ExpiresAt });

        builder.ToTable("MenuItemStockLots");
    }
}
