using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sugarista.Domain.Entities;

namespace Sugarista.Infrastructure.Data.Configurations;

public class PayrollPeriodConfiguration : IEntityTypeConfiguration<PayrollPeriod>
{
    public void Configure(EntityTypeBuilder<PayrollPeriod> builder)
    {
        builder.Property(period => period.Note)
            .HasMaxLength(500);

        builder.HasIndex(period => new { period.ShamsiYear, period.ShamsiMonth })
            .IsUnique();

        builder.HasMany(period => period.Entries)
            .WithOne(entry => entry.Period)
            .HasForeignKey(entry => entry.PeriodId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
