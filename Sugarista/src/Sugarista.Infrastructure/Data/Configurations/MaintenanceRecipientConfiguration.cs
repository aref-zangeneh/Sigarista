using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sugarista.Domain.Entities;

namespace Sugarista.Infrastructure.Data.Configurations;

public class MaintenanceRecipientConfiguration : IEntityTypeConfiguration<MaintenanceRecipient>
{
    public void Configure(EntityTypeBuilder<MaintenanceRecipient> builder)
    {
        builder.Property(recipient => recipient.Channel)
            .HasConversion<string>()
            .HasMaxLength(10);

        builder.Property(recipient => recipient.Target)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(recipient => recipient.Label)
            .HasMaxLength(100);

        builder.HasIndex(recipient => new { recipient.ScheduleId, recipient.Channel });
    }
}
