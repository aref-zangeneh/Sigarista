using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sugarista.Domain.Entities;

namespace Sugarista.Infrastructure.Data.Configurations;

public class AdminActivityLogConfiguration : IEntityTypeConfiguration<AdminActivityLog>
{
    public void Configure(EntityTypeBuilder<AdminActivityLog> builder)
    {
        builder.Property(entry => entry.ActorUsername)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(entry => entry.ActorDisplayName)
            .HasMaxLength(150);

        builder.Property(entry => entry.Action)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(entry => entry.SectionId)
            .HasMaxLength(80)
            .IsRequired();

        builder.Property(entry => entry.SectionTitle)
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(entry => entry.EntityLabel)
            .HasMaxLength(200);

        builder.Property(entry => entry.Description)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(entry => entry.ChangedFields)
            .HasMaxLength(500);

        builder.Property(entry => entry.HttpMethod)
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(entry => entry.Path)
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(entry => entry.IpAddress)
            .HasMaxLength(60);

        builder.Property(entry => entry.UserAgent)
            .HasMaxLength(300);

        builder.HasIndex(entry => entry.OccurredAt);

        builder.HasIndex(entry => new { entry.ActorUsername, entry.OccurredAt });

        builder.HasIndex(entry => new { entry.SectionId, entry.OccurredAt });
    }
}
