using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sugarista.Domain.Entities;

namespace Sugarista.Infrastructure.Data.Configurations;

public class MenuModifierConfiguration : IEntityTypeConfiguration<MenuModifier>
{
    public void Configure(EntityTypeBuilder<MenuModifier> builder)
    {
        builder.Property(modifier => modifier.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(modifier => modifier.PersianName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(modifier => modifier.EnglishName)
            .HasMaxLength(200)
            .IsRequired();
    }
}

