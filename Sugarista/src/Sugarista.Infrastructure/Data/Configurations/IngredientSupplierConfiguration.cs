using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sugarista.Domain.Entities;

namespace Sugarista.Infrastructure.Data.Configurations;

public class IngredientSupplierConfiguration : IEntityTypeConfiguration<IngredientSupplier>
{
    public void Configure(EntityTypeBuilder<IngredientSupplier> builder)
    {
        builder.HasIndex(i => new { i.IngredientId, i.SupplierId })
            .IsUnique();

        builder.HasOne(i => i.Ingredient)
            .WithMany(i => i.IngredientSuppliers)
            .HasForeignKey(i => i.IngredientId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(i => i.Supplier)
            .WithMany(s => s.IngredientSuppliers)
            .HasForeignKey(i => i.SupplierId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
