using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sugarista.Domain.Entities;

namespace Sugarista.Infrastructure.Data.Configurations;

public class MenuItemOptionGroupConfiguration : IEntityTypeConfiguration<MenuItemOptionGroup>
{
    public void Configure(EntityTypeBuilder<MenuItemOptionGroup> builder)
    {
        builder.Property(group => group.PersianName)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(group => group.EnglishName)
            .HasMaxLength(150);

        builder.Property(group => group.SelectionMode)
            .HasMaxLength(10)
            .IsRequired();

        builder.HasIndex(group => new { group.MenuItemId, group.DisplayOrder });

        builder.HasOne(group => group.MenuItem)
            .WithMany()
            .HasForeignKey(group => group.MenuItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(group => group.Options)
            .WithOne(option => option.Group)
            .HasForeignKey(option => option.GroupId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class MenuItemOptionConfiguration : IEntityTypeConfiguration<MenuItemOption>
{
    public void Configure(EntityTypeBuilder<MenuItemOption> builder)
    {
        builder.Property(option => option.PersianName)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(option => option.EnglishName)
            .HasMaxLength(150);

        builder.Property(option => option.PriceDelta)
            .HasColumnType("numeric(18,2)");

        builder.HasIndex(option => new { option.GroupId, option.DisplayOrder });

        builder.HasMany(option => option.Ingredients)
            .WithOne(ingredientRow => ingredientRow.Option)
            .HasForeignKey(ingredientRow => ingredientRow.OptionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class MenuItemOptionIngredientConfiguration : IEntityTypeConfiguration<MenuItemOptionIngredient>
{
    public void Configure(EntityTypeBuilder<MenuItemOptionIngredient> builder)
    {
        builder.Property(ingredientRow => ingredientRow.Quantity)
            .HasColumnType("numeric(18,3)");

        builder.HasIndex(ingredientRow => new { ingredientRow.OptionId, ingredientRow.IngredientId });

        builder.HasOne(ingredientRow => ingredientRow.Ingredient)
            .WithMany()
            .HasForeignKey(ingredientRow => ingredientRow.IngredientId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
