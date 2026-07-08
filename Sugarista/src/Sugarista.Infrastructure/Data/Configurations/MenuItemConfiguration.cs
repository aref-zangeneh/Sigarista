using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sugarista.Domain.Entities;

namespace Sugarista.Infrastructure.Data.Configurations;

public class MenuItemConfiguration : IEntityTypeConfiguration<MenuItem>
{
    public void Configure(EntityTypeBuilder<MenuItem> builder)
    {
        builder.Property(item => item.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(item => item.Description)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(item => item.Badge)
            .HasMaxLength(100);

        builder.Property(item => item.Accent)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(item => item.ShortLabel)
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(item => item.Tags)
            .HasColumnType("text[]");
    }
}
