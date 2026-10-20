using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sugarista.Domain.Entities;

namespace Sugarista.Infrastructure.Data.Configurations;

public class ExportJobConfiguration : IEntityTypeConfiguration<ExportJob>
{
    public void Configure(EntityTypeBuilder<ExportJob> builder)
    {
        builder.Property(job => job.Kind)
            .HasMaxLength(60)
            .IsRequired();

        builder.Property(job => job.RequestedBy)
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(job => job.FileName)
            .HasMaxLength(260);

        builder.Property(job => job.StoragePath)
            .HasMaxLength(600);

        builder.Property(job => job.ErrorMessage)
            .HasMaxLength(1020);

        builder.HasIndex(job => job.Status);
    }
}
