using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sugarista.Domain.Entities;

namespace Sugarista.Infrastructure.Data.Configurations;

public class PayrollEntryConfiguration : IEntityTypeConfiguration<PayrollEntry>
{
    public void Configure(EntityTypeBuilder<PayrollEntry> builder)
    {
        builder.Property(entry => entry.EmployeeName)
            .HasMaxLength(160)
            .IsRequired();

        builder.Property(entry => entry.EmployeePosition)
            .HasMaxLength(160)
            .IsRequired();

        builder.Property(entry => entry.BasePay).HasPrecision(18, 2);
        builder.Property(entry => entry.OvertimePay).HasPrecision(18, 2);
        builder.Property(entry => entry.CommissionSalesAmount).HasPrecision(18, 2);
        builder.Property(entry => entry.CommissionPercent).HasPrecision(9, 4);
        builder.Property(entry => entry.CommissionPay).HasPrecision(18, 2);
        builder.Property(entry => entry.Bonus).HasPrecision(18, 2);
        builder.Property(entry => entry.Deductions).HasPrecision(18, 2);
        builder.Property(entry => entry.Advance).HasPrecision(18, 2);

        builder.Property(entry => entry.Note)
            .HasMaxLength(800);

        builder.HasIndex(entry => new { entry.PeriodId, entry.EmployeeId })
            .IsUnique();

        builder.HasIndex(entry => entry.Status);
    }
}
