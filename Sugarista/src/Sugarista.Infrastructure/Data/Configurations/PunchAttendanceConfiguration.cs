using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sugarista.Domain.Entities;

namespace Sugarista.Infrastructure.Data.Configurations;

public class PunchAttendanceConfiguration : IEntityTypeConfiguration<PunchAttendance>
{
    public void Configure(EntityTypeBuilder<PunchAttendance> builder)
    {
        builder.HasIndex(attendance => new { attendance.EmployeeId, attendance.WorkDate });

        builder.HasIndex(attendance => attendance.PunchInAt);

        builder.HasOne(attendance => attendance.Employee)
            .WithMany(employee => employee.Punches)
            .HasForeignKey(attendance => attendance.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
