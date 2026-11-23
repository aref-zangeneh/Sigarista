using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sugarista.Domain.Entities;

namespace Sugarista.Infrastructure.Data.Configurations;

public class CustomerOtpCodeConfiguration : IEntityTypeConfiguration<CustomerOtpCode>
{
    public void Configure(EntityTypeBuilder<CustomerOtpCode> builder)
    {
        builder.HasKey(entry => entry.Id);

        builder.Property(entry => entry.PhoneNumber).HasMaxLength(20).IsRequired();
        builder.Property(entry => entry.CodeHash).HasMaxLength(64).IsRequired();
        builder.Property(entry => entry.Salt).HasMaxLength(32).IsRequired();
        builder.Property(entry => entry.RequestIp).HasMaxLength(64);

        builder.HasIndex(entry => new { entry.PhoneNumber, entry.CreatedAtUtc });
        builder.HasIndex(entry => entry.ExpiresAtUtc);
    }
}
