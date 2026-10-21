using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sugarista.Domain.Entities;

namespace Sugarista.Infrastructure.Data.Configurations;

public class EmployeePayPlanConfiguration : IEntityTypeConfiguration<EmployeePayPlan>
{
    public void Configure(EntityTypeBuilder<EmployeePayPlan> builder)
    {
        builder.Property(plan => plan.SalaryAmount)
            .HasPrecision(18, 2);

        builder.Property(plan => plan.HourlyRate)
            .HasPrecision(18, 2);

        builder.Property(plan => plan.ShiftRate)
            .HasPrecision(18, 2);

        builder.Property(plan => plan.CommissionPercent)
            .HasPrecision(9, 4);

        builder.Property(plan => plan.OvertimeMultiplier)
            .HasPrecision(9, 4);

        builder.Property(plan => plan.Notes)
            .HasMaxLength(800);

        builder.HasIndex(plan => new { plan.EmployeeId, plan.IsActive });

        builder.HasIndex(plan => plan.EmployeeId);
    }
}
