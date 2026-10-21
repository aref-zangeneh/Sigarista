using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sugarista.Domain.Entities;

namespace Sugarista.Infrastructure.Data.Configurations;

public class CafeAssetPhotoConfiguration : IEntityTypeConfiguration<CafeAssetPhoto>
{
    public void Configure(EntityTypeBuilder<CafeAssetPhoto> builder)
    {
        builder.Property(photo => photo.ImageUrl)
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(photo => photo.Caption)
            .HasMaxLength(150);

        builder.HasIndex(photo => new { photo.AssetId, photo.IsPrimary });
    }
}
