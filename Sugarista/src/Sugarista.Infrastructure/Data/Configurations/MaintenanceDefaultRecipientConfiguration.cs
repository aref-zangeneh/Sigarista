using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sugarista.Domain.Entities;

namespace Sugarista.Infrastructure.Data.Configurations;

public class MaintenanceDefaultRecipientConfiguration : IEntityTypeConfiguration<MaintenanceDefaultRecipient>
{
    public void Configure(EntityTypeBuilder<MaintenanceDefaultRecipient> builder)
    {
        builder.Property(recipient => recipient.Channel)
            .HasConversion<string>()
            .HasMaxLength(10);

        builder.Property(recipient => recipient.Target)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(recipient => recipient.Label)
            .HasMaxLength(100);

        builder.HasIndex(recipient => new { recipient.Channel, recipient.Target })
            .IsUnique();
    }
}
