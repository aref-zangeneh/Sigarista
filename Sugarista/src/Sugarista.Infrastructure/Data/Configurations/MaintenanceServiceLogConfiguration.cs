using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sugarista.Domain.Entities;

namespace Sugarista.Infrastructure.Data.Configurations;

public class MaintenanceServiceLogConfiguration : IEntityTypeConfiguration<MaintenanceServiceLog>
{
    public void Configure(EntityTypeBuilder<MaintenanceServiceLog> builder)
    {
        builder.Property(log => log.Cost)
            .HasPrecision(18, 2);

        builder.Property(log => log.PerformedBy)
            .HasMaxLength(120);

        builder.Property(log => log.Description)
            .HasMaxLength(600);

        builder.HasOne(log => log.Schedule)
            .WithMany()
            .HasForeignKey(log => log.ScheduleId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(log => new { log.EquipmentId, log.ServicedOn });
    }
}
