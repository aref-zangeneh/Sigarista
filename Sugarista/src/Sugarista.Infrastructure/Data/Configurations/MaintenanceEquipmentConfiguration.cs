using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sugarista.Domain.Entities;

namespace Sugarista.Infrastructure.Data.Configurations;

public class MaintenanceEquipmentConfiguration : IEntityTypeConfiguration<MaintenanceEquipment>
{
    public void Configure(EntityTypeBuilder<MaintenanceEquipment> builder)
    {
        builder.Property(entity => entity.PersianName)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(entity => entity.Category)
            .HasMaxLength(60);

        builder.Property(entity => entity.BrandModel)
            .HasMaxLength(120);

        builder.Property(entity => entity.SerialNumber)
            .HasMaxLength(80);

        builder.Property(entity => entity.Location)
            .HasMaxLength(100);

        builder.Property(entity => entity.Notes)
            .HasMaxLength(500);

        builder.Property(entity => entity.OutOfServiceNote)
            .HasMaxLength(300);

        builder.HasMany(entity => entity.Schedules)
            .WithOne(schedule => schedule.Equipment)
            .HasForeignKey(schedule => schedule.EquipmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(entity => entity.ServiceLogs)
            .WithOne(log => log.Equipment)
            .HasForeignKey(log => log.EquipmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(entity => new { entity.Category, entity.IsActive });
    }
}
