using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sugarista.Domain.Entities;

namespace Sugarista.Infrastructure.Data.Configurations;

public class RecipeItemConfiguration : IEntityTypeConfiguration<RecipeItem>
{
    public void Configure(EntityTypeBuilder<RecipeItem> builder)
    {
        builder.Property(r => r.Quantity)
            .HasColumnType("numeric(10,3)")
            .IsRequired();

        builder.HasOne(r => r.MenuItem)
            .WithMany(m => m.RecipeItems)
            .HasForeignKey(r => r.MenuItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.Ingredient)
            .WithMany(i => i.RecipeItems)
            .HasForeignKey(r => r.IngredientId)
            .OnDelete(DeleteBehavior.Restrict);

        // Enforce uniqueness: one ingredient appears at most once per menu item recipe
        builder.HasIndex(r => new { r.MenuItemId, r.IngredientId }).IsUnique();
    }
}
