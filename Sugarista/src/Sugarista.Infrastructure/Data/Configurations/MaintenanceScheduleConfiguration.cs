using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sugarista.Domain.Entities;

namespace Sugarista.Infrastructure.Data.Configurations;

public class MaintenanceScheduleConfiguration : IEntityTypeConfiguration<MaintenanceSchedule>
{
    public void Configure(EntityTypeBuilder<MaintenanceSchedule> builder)
    {
        builder.Property(schedule => schedule.Title)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(schedule => schedule.IntervalDays);

        builder.HasIndex(schedule => new { schedule.NextDueDate, schedule.IsActive });

        builder.HasMany(schedule => schedule.Recipients)
            .WithOne(recipient => recipient.Schedule)
            .HasForeignKey(recipient => recipient.ScheduleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
