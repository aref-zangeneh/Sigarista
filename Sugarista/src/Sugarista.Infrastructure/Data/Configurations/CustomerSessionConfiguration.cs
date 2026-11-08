using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sugarista.Domain.Entities;

namespace Sugarista.Infrastructure.Data.Configurations;

public class CustomerSessionConfiguration : IEntityTypeConfiguration<CustomerSession>
{
    public void Configure(EntityTypeBuilder<CustomerSession> builder)
    {
        builder.HasKey(entry => entry.Id);

        builder.Property(entry => entry.TokenHash).HasMaxLength(64);
        builder.HasIndex(entry => entry.TokenHash).IsUnique();

        builder.HasIndex(entry => entry.CustomerId);

        builder.HasOne(entry => entry.Customer)
            .WithMany()
            .HasForeignKey(entry => entry.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
