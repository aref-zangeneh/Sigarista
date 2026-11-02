using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sugarista.Domain.Entities;

namespace Sugarista.Infrastructure.Data.Configurations;

public class PrintJobConfiguration : IEntityTypeConfiguration<PrintJob>
{
    public void Configure(EntityTypeBuilder<PrintJob> builder)
    {
        builder.HasKey(job => job.Id);

        builder.Property(job => job.OrderTrackingCode)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(job => job.PayloadJson)
            .IsRequired();

        builder.Property(job => job.LastError)
            .HasMaxLength(2000);

        builder.HasIndex(job => job.OrderId);

        // The agent claims jobs with Status IN (Pending, stale Dispatched) ordered by CreatedAt.
        builder.HasIndex(job => new { job.Status, job.CreatedAt });

        builder.ToTable("PrintJobs");
    }
}
