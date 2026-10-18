using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sugarista.Domain.Entities;

namespace Sugarista.Infrastructure.Data.Configurations;

public class InventoryPurchaseConfiguration : IEntityTypeConfiguration<InventoryPurchase>
{
    public void Configure(EntityTypeBuilder<InventoryPurchase> builder)
    {
        builder.Property(p => p.Quantity)
            .HasColumnType("numeric(12,3)")
            .IsRequired();

        builder.Property(p => p.PackageSize)
            .HasColumnType("numeric(12,3)");

        builder.Property(p => p.RemainingQuantity)
            .HasColumnType("numeric(12,3)")
            .IsRequired();

        builder.Property(p => p.PurchaseUnitPrice)
            .HasColumnType("numeric(18,2)");

        builder.Property(p => p.ShippingFee)
            .HasColumnType("numeric(18,2)")
            .HasDefaultValue(0m);

        builder.Property(p => p.TaxAmount)
            .HasColumnType("numeric(18,2)")
            .HasDefaultValue(0m);

        builder.Property(p => p.IncidentalCost)
            .HasColumnType("numeric(18,2)")
            .HasDefaultValue(0m);

        builder.Property(p => p.Description)
            .HasMaxLength(1000);

        builder.Property(p => p.DocumentImageUrl)
            .HasMaxLength(2000);

        builder.HasOne(p => p.Ingredient)
            .WithMany()
            .HasForeignKey(p => p.IngredientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.Supplier)
            .WithMany()
            .HasForeignKey(p => p.SupplierId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(p => p.StockMovement)
            .WithMany()
            .HasForeignKey(p => p.StockMovementId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(p => new { p.IngredientId, p.ExpirationDate });
        builder.HasIndex(p => p.ExpirationDate);
        builder.HasIndex(p => p.ReceivedAt);
        builder.HasIndex(p => p.SupplierId);
    }
}
