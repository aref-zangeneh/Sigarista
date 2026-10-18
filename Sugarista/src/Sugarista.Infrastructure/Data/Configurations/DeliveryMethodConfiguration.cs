using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sugarista.Domain.Entities;

namespace Sugarista.Infrastructure.Data.Configurations;

public class DeliveryMethodConfiguration : IEntityTypeConfiguration<DeliveryMethod>
{
    public void Configure(EntityTypeBuilder<DeliveryMethod> builder)
    {
        builder.Property(method => method.PersianName)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(method => method.EnglishName)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(method => method.Description)
            .HasMaxLength(1000);

        builder.Property(method => method.DeliveryFee)
            .HasColumnType("numeric(18,2)");

    }
}

public class DeliveryMethodPackagingConfiguration : IEntityTypeConfiguration<DeliveryMethodPackaging>
{
    public void Configure(EntityTypeBuilder<DeliveryMethodPackaging> builder)
    {
        builder.Property(line => line.QuantityPerOrder)
            .HasColumnType("numeric(12,3)");

        builder.HasIndex(line => new { line.DeliveryMethodId, line.IngredientId })
            .IsUnique();

        builder.HasOne(line => line.DeliveryMethod)
            .WithMany(method => method.Packagings)
            .HasForeignKey(line => line.DeliveryMethodId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(line => line.Ingredient)
            .WithMany()
            .HasForeignKey(line => line.IngredientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(line => line.IngredientId);
    }
}
