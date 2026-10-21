using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sugarista.Domain.Entities;

namespace Sugarista.Infrastructure.Data.Configurations;

public class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.Property(employee => employee.PersianName)
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(employee => employee.EnglishName)
            .HasMaxLength(120);

        builder.Property(employee => employee.PhoneNumber)
            .HasMaxLength(30);

        builder.Property(employee => employee.NationalId)
            .HasMaxLength(30);

        builder.Property(employee => employee.Position)
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(employee => employee.Notes)
            .HasMaxLength(800);

        builder.HasIndex(employee => employee.IsActive);

        builder.HasMany(employee => employee.PayPlans)
            .WithOne()
            .HasForeignKey(plan => plan.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(employee => employee.WorkLogs)
            .WithOne()
            .HasForeignKey(log => log.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(employee => employee.PayrollEntries)
            .WithOne()
            .HasForeignKey(entry => entry.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
