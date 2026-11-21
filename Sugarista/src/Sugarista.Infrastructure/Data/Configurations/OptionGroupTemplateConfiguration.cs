using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sugarista.Domain.Entities;

namespace Sugarista.Infrastructure.Data.Configurations;

public class OptionGroupTemplateConfiguration : IEntityTypeConfiguration<OptionGroupTemplate>
{
    public void Configure(EntityTypeBuilder<OptionGroupTemplate> builder)
    {
        builder.Property(template => template.CategoryLabel)
            .HasMaxLength(100);

        builder.Property(template => template.PersianName)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(template => template.EnglishName)
            .HasMaxLength(150);

        builder.Property(template => template.SelectionMode)
            .HasMaxLength(10)
            .IsRequired();

        builder.HasIndex(template => new { template.CategoryLabel, template.DisplayOrder });

        builder.HasMany(template => template.Options)
            .WithOne(option => option.Template)
            .HasForeignKey(option => option.TemplateId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class OptionGroupTemplateItemConfiguration : IEntityTypeConfiguration<OptionGroupTemplateItem>
{
    public void Configure(EntityTypeBuilder<OptionGroupTemplateItem> builder)
    {
        builder.Property(option => option.PersianName)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(option => option.EnglishName)
            .HasMaxLength(150);

        builder.Property(option => option.PriceDelta)
            .HasColumnType("numeric(18,2)");

        builder.HasIndex(option => new { option.TemplateId, option.DisplayOrder });

        builder.HasMany(option => option.Ingredients)
            .WithOne(ingredientRow => ingredientRow.TemplateItem)
            .HasForeignKey(ingredientRow => ingredientRow.TemplateItemId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class OptionGroupTemplateItemIngredientConfiguration : IEntityTypeConfiguration<OptionGroupTemplateItemIngredient>
{
    public void Configure(EntityTypeBuilder<OptionGroupTemplateItemIngredient> builder)
    {
        builder.Property(ingredientRow => ingredientRow.Quantity)
            .HasColumnType("numeric(18,3)");

        builder.HasIndex(ingredientRow => new { ingredientRow.TemplateItemId, ingredientRow.IngredientId });

        builder.HasOne(ingredientRow => ingredientRow.Ingredient)
            .WithMany()
            .HasForeignKey(ingredientRow => ingredientRow.IngredientId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
