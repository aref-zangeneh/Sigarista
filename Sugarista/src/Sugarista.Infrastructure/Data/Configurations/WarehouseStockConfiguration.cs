using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sugarista.Domain.Entities;

namespace Sugarista.Infrastructure.Data.Configurations;

public class WarehouseStockConfiguration : IEntityTypeConfiguration<WarehouseStock>
{
    public void Configure(EntityTypeBuilder<WarehouseStock> builder)
    {
        builder.Property(s => s.Quantity)
            .HasColumnType("numeric(12,3)")
            .IsRequired();

        builder.Property(s => s.MinAlertQuantity)
            .HasColumnType("numeric(12,3)")
            .IsRequired();

        builder.HasOne(s => s.Warehouse)
            .WithMany()
            .HasForeignKey(s => s.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.Ingredient)
            .WithMany()
            .HasForeignKey(s => s.IngredientId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => new { s.WarehouseId, s.IngredientId })
            .IsUnique();

        builder.Ignore(s => s.IsLowStock);
    }
}
