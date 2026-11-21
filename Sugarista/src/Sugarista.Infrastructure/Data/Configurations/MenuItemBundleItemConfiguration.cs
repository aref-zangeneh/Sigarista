using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sugarista.Domain.Entities;

namespace Sugarista.Infrastructure.Data.Configurations;

public class MenuItemBundleItemConfiguration : IEntityTypeConfiguration<MenuItemBundleItem>
{
    public void Configure(EntityTypeBuilder<MenuItemBundleItem> builder)
    {
        builder.ToTable("MenuItemBundleItems");

        builder.Property(item => item.SizeKey)
            .HasMaxLength(20);

        builder.Property(item => item.Quantity)
            .HasDefaultValue(1)
            .IsRequired();

        builder.Property(item => item.DisplayOrder)
            .HasDefaultValue(0)
            .IsRequired();

        builder.HasOne(item => item.BundleMenuItem)
            .WithMany(menuItem => menuItem.BundleItems)
            .HasForeignKey(item => item.BundleMenuItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(item => item.ComponentMenuItem)
            .WithMany()
            .HasForeignKey(item => item.ComponentMenuItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(item => item.BundleMenuItemId);
        builder.HasIndex(item => item.ComponentMenuItemId);
    }
}
