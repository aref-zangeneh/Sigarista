using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sugarista.Domain.Entities;

namespace Sugarista.Infrastructure.Data.Configurations;

public class BackupCloudTargetConfiguration : IEntityTypeConfiguration<BackupCloudTarget>
{
    public void Configure(EntityTypeBuilder<BackupCloudTarget> builder)
    {
        builder.ToTable("BackupCloudTargets");
        builder.Property(x => x.Name).IsRequired().HasMaxLength(120);
        builder.Property(x => x.EndpointUrl).IsRequired().HasMaxLength(300);
        builder.Property(x => x.BucketName).HasMaxLength(120);
        builder.Property(x => x.Region).HasMaxLength(60);
        builder.Property(x => x.AccessKey).HasMaxLength(200);
        builder.Property(x => x.SecretKey).HasMaxLength(300);
        builder.Property(x => x.KeyPrefix).HasMaxLength(120);
        builder.Property(x => x.LastTestMessage).HasMaxLength(400);
    }
}

public class BackupJobConfiguration : IEntityTypeConfiguration<BackupJob>
{
    public void Configure(EntityTypeBuilder<BackupJob> builder)
    {
        builder.ToTable("BackupJobs");
        builder.Property(x => x.Name).IsRequired().HasMaxLength(140);
        builder.Property(x => x.LastRunStatus).HasMaxLength(32);

        builder.HasOne(x => x.CloudTarget)
            .WithMany()
            .HasForeignKey(x => x.CloudTargetId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.IsEnabled);
        builder.HasIndex(x => x.NextRunAt);
    }
}

public class BackupRecordConfiguration : IEntityTypeConfiguration<BackupRecord>
{
    public void Configure(EntityTypeBuilder<BackupRecord> builder)
    {
        builder.ToTable("BackupRecords");
        builder.Property(x => x.BackupCode).IsRequired().HasMaxLength(40);
        builder.Property(x => x.JobName).HasMaxLength(140);
        builder.Property(x => x.TriggerType).IsRequired().HasMaxLength(32);
        builder.Property(x => x.FileName).HasMaxLength(260);
        builder.Property(x => x.Sha256Checksum).HasMaxLength(80);
        builder.Property(x => x.DatabaseEngine).HasMaxLength(60);
        builder.Property(x => x.ErrorMessage).HasMaxLength(1000);
        builder.Property(x => x.CreatedByName).HasMaxLength(150);
        builder.Property(x => x.CloudTargetName).HasMaxLength(120);
        builder.Property(x => x.CloudObjectKey).HasMaxLength(300);
        builder.Property(x => x.CloudErrorMessage).HasMaxLength(1000);

        builder.HasIndex(x => x.StartedAt);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.JobId);
    }
}
