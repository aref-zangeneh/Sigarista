using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sugarista.Domain.Entities;

namespace Sugarista.Infrastructure.Data.Configurations;

public class EmployeeShiftPlanConfiguration : IEntityTypeConfiguration<EmployeeShiftPlan>
{
    public void Configure(EntityTypeBuilder<EmployeeShiftPlan> builder)
    {
        builder.HasIndex(plan => new { plan.EmployeeId, plan.Weekday })
            .IsUnique();

        builder.HasOne(plan => plan.Employee)
            .WithMany(employee => employee.ShiftPlans)
            .HasForeignKey(plan => plan.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
