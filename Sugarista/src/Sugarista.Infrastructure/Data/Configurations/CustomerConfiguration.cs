using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sugarista.Domain.Entities;

namespace Sugarista.Infrastructure.Data.Configurations;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.Property(customer => customer.PersianName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(customer => customer.PhoneNumber)
            .HasMaxLength(30)
            .IsRequired();

        builder.HasIndex(customer => customer.PhoneNumber)
            .IsUnique();
    }
}
