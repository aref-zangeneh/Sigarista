using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sugarista.Domain.Entities;

namespace Sugarista.Infrastructure.Data.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.Property(category => category.Slug)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(category => category.Slug)
            .IsUnique();

        builder.Property(category => category.Name)
            .HasMaxLength(150)
            .IsRequired();

        builder.HasMany(category => category.MenuItems)
            .WithOne(item => item.Category)
            .HasForeignKey(item => item.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
