using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sugarista.Domain.Entities;

namespace Sugarista.Infrastructure.Data.Configurations;

public class SupplierOrderListItemConfiguration : IEntityTypeConfiguration<SupplierOrderListItem>
{
    public void Configure(EntityTypeBuilder<SupplierOrderListItem> builder)
    {
        builder.ToTable("SupplierOrderListItems");

        builder.Property(x => x.ItemName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.CategoryName)
            .HasMaxLength(120);

        builder.Property(x => x.SupplierNameOverride)
            .HasMaxLength(200);

        builder.Property(x => x.SupplierPhoneOverride)
            .HasMaxLength(40);

        builder.Property(x => x.Quantity)
            .HasPrecision(18, 3);

        builder.Property(x => x.Unit)
            .IsRequired()
            .HasMaxLength(40);

        builder.Property(x => x.Notes)
            .HasMaxLength(500);

        builder.Property(x => x.BatchCode)
            .HasMaxLength(40);

        builder.HasOne(x => x.MenuItem)
            .WithMany()
            .HasForeignKey(x => x.MenuItemId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.Supplier)
            .WithMany()
            .HasForeignKey(x => x.SupplierId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.SupplierId);
        builder.HasIndex(x => x.MenuItemId);
        builder.HasIndex(x => x.BatchCode);
    }
}
