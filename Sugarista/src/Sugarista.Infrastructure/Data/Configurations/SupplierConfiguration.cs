using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sugarista.Domain.Entities;

namespace Sugarista.Infrastructure.Data.Configurations;

public class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> builder)
    {
        builder.Property(s => s.PersianName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(s => s.PhoneNumber)
            .HasMaxLength(40);

        builder.Property(s => s.ContactName)
            .HasMaxLength(120);

        builder.Property(s => s.Notes)
            .HasMaxLength(500);

        builder.HasIndex(s => s.PersianName)
            .IsUnique();

        builder.HasIndex(s => s.DisplayOrder);
    }
}
