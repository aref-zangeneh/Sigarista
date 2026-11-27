using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sugarista.Domain.Entities;

namespace Sugarista.Infrastructure.Data.Configurations;

public class StaffTaskConfiguration : IEntityTypeConfiguration<StaffTask>
{
    public void Configure(EntityTypeBuilder<StaffTask> builder)
    {
        builder.ToTable("StaffTasks");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Title)
            .HasMaxLength(160)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(800);

        builder.Property(x => x.Category)
            .HasMaxLength(60)
            .IsRequired();

        builder.Property(x => x.Priority)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.RecurrenceType)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.DaysOfWeekMask)
            .IsRequired();

        builder.Property(x => x.RaiseTimeOfDay)
            .IsRequired();

        builder.Property(x => x.RequireNoteOnComplete)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .HasDefaultValue(true)
            .IsRequired();

        builder.HasOne(x => x.AssignedToAdminUser)
            .WithMany()
            .HasForeignKey(x => x.AssignedToAdminUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.AssignedToAdminUserId, x.IsActive });
        builder.HasIndex(x => new { x.IsActive, x.RaiseTimeOfDay });
    }
}

public class StaffTaskCompletionConfiguration : IEntityTypeConfiguration<StaffTaskCompletion>
{
    public void Configure(EntityTypeBuilder<StaffTaskCompletion> builder)
    {
        builder.ToTable("StaffTaskCompletions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.CompletedByDisplayName)
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(x => x.Note)
            .HasMaxLength(500);

        builder.HasOne(x => x.StaffTask)
            .WithMany(t => t.Completions)
            .HasForeignKey(x => x.StaffTaskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.StaffTaskId, x.OccurrenceDate }).IsUnique();
        builder.HasIndex(x => x.CompletedAtUtc);
        builder.HasIndex(x => x.CompletedByAdminUserId);
    }
}

public class StaffTaskActivityLogConfiguration : IEntityTypeConfiguration<StaffTaskActivityLog>
{
    public void Configure(EntityTypeBuilder<StaffTaskActivityLog> builder)
    {
        builder.ToTable("StaffTaskActivityLogs");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.TaskTitleSnapshot)
            .HasMaxLength(180)
            .IsRequired();

        builder.Property(x => x.AssignedToDisplayName)
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(x => x.Action)
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(x => x.ActorDisplayName)
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(x => x.Details)
            .HasMaxLength(600);

        builder.HasIndex(x => x.OccurredAtUtc);
        builder.HasIndex(x => x.StaffTaskId);
    }
}
