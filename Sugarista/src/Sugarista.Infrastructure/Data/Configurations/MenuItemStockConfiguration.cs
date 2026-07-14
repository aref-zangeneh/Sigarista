using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sugarista.Domain.Entities;

namespace Sugarista.Infrastructure.Data.Configurations;

public class MenuItemStockConfiguration : IEntityTypeConfiguration<MenuItemStock>
{
    public void Configure(EntityTypeBuilder<MenuItemStock> builder)
    {
        builder.Property(s => s.CurrentQuantity)
            .HasColumnType("numeric(12,3)")
            .IsRequired();

        builder.Property(s => s.MinAlertQuantity)
            .HasColumnType("numeric(12,3)")
            .IsRequired();

        builder.HasOne(s => s.MenuItem)
            .WithOne()
            .HasForeignKey<MenuItemStock>(s => s.MenuItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => s.MenuItemId).IsUnique();

        builder.Ignore(s => s.IsLowStock);
    }
}
