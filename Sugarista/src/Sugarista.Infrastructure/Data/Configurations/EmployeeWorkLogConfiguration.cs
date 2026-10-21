using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sugarista.Domain.Entities;

namespace Sugarista.Infrastructure.Data.Configurations;

public class EmployeeWorkLogConfiguration : IEntityTypeConfiguration<EmployeeWorkLog>
{
    public void Configure(EntityTypeBuilder<EmployeeWorkLog> builder)
    {
        builder.Property(log => log.Note)
            .HasMaxLength(500);

        builder.Property(log => log.WorkedHours)
            .HasPrecision(6, 2);

        builder.Property(log => log.OvertimeHours)
            .HasPrecision(6, 2);

        builder.HasIndex(log => new { log.EmployeeId, log.WorkDate })
            .IsUnique();

        builder.HasIndex(log => log.WorkDate);
    }
}
