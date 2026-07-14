using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sugarista.Domain.Entities;

namespace Sugarista.Infrastructure.Data.Configurations;

public class InventoryStockConfiguration : IEntityTypeConfiguration<InventoryStock>
{
    public void Configure(EntityTypeBuilder<InventoryStock> builder)
    {
        builder.Property(s => s.CurrentQuantity)
            .HasColumnType("numeric(12,3)")
            .IsRequired();

        builder.Property(s => s.MinAlertQuantity)
            .HasColumnType("numeric(12,3)")
            .IsRequired();

        builder.HasOne(s => s.Ingredient)
            .WithOne(i => i.Stock)
            .HasForeignKey<InventoryStock>(s => s.IngredientId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => s.IngredientId).IsUnique();

        builder.Ignore(s => s.IsLowStock);
    }
}
