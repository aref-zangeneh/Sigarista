using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sugarista.Domain.Entities;

namespace Sugarista.Infrastructure.Data.Configurations;

public class IngredientConfiguration : IEntityTypeConfiguration<Ingredient>
{
    public void Configure(EntityTypeBuilder<Ingredient> builder)
    {
        builder.Property(i => i.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(i => i.PersianName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(i => i.EnglishName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(i => i.Unit)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(i => i.Description)
            .HasMaxLength(500);

        builder.Property(i => i.InventoryLocation)
            .HasMaxLength(20)
            .IsRequired()
            .HasDefaultValue("main");

        builder.Property(i => i.UnitCost)
            .HasColumnType("numeric(18,2)")
            .HasDefaultValue(0m);

        builder.Property(i => i.Kind)
            .HasMaxLength(20)
            .IsRequired()
            .HasDefaultValue("ingredient");

        builder.HasIndex(i => i.DisplayOrder);
        builder.HasIndex(i => i.Kind);
    }
}
