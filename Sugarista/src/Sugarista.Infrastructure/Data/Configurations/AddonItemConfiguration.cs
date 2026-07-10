using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sugarista.Domain.Entities;

namespace Sugarista.Infrastructure.Data.Configurations;

public class AddonItemConfiguration : IEntityTypeConfiguration<AddonItem>
{
    public void Configure(EntityTypeBuilder<AddonItem> builder)
    {
        builder.Property(item => item.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(item => item.PersianName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(item => item.EnglishName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(item => item.Description)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(item => item.Tags)
            .HasColumnType("text[]");
    }
}

