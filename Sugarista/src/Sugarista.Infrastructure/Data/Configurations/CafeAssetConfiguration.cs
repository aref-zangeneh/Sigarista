using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sugarista.Domain.Entities;

namespace Sugarista.Infrastructure.Data.Configurations;

public class CafeAssetConfiguration : IEntityTypeConfiguration<CafeAsset>
{
    public void Configure(EntityTypeBuilder<CafeAsset> builder)
    {
        builder.Property(entity => entity.PersianName)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(entity => entity.EnglishName)
            .HasMaxLength(150);

        builder.Property(entity => entity.Category)
            .HasMaxLength(60)
            .IsRequired();

        builder.Property(entity => entity.SerialNumber)
            .HasMaxLength(80);

        builder.Property(entity => entity.Location)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(entity => entity.SupplierName)
            .HasMaxLength(120);

        builder.Property(entity => entity.Note)
            .HasMaxLength(500);

        builder.Property(entity => entity.DisposalNote)
            .HasMaxLength(300);

        builder.Property(entity => entity.UnitPrice)
            .HasPrecision(18, 2);

        builder.HasMany(entity => entity.Photos)
            .WithOne(photo => photo.CafeAsset)
            .HasForeignKey(photo => photo.AssetId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(entity => new { entity.Category, entity.IsActive });
    }
}
