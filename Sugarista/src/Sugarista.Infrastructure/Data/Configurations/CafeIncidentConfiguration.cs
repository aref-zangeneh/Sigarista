using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sugarista.Domain.Entities;

namespace Sugarista.Infrastructure.Data.Configurations;

public class CafeIncidentConfiguration : IEntityTypeConfiguration<CafeIncident>
{
    public void Configure(EntityTypeBuilder<CafeIncident> builder)
    {
        builder.ToTable("CafeIncidents");

        builder.Property(x => x.IncidentCode)
            .IsRequired()
            .HasMaxLength(32);

        builder.Property(x => x.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Location)
            .HasMaxLength(150);

        builder.Property(x => x.Description)
            .HasMaxLength(2000);

        builder.Property(x => x.ImpactSummary)
            .HasMaxLength(1000);

        builder.Property(x => x.EstimatedLossAmount)
            .HasPrecision(18, 2);

        builder.Property(x => x.ResolutionNotes)
            .HasMaxLength(2000);

        builder.Property(x => x.ReportedByName)
            .HasMaxLength(150);

        builder.Property(x => x.ResolvedByName)
            .HasMaxLength(150);

        builder.Property(x => x.PhotoUrl)
            .HasMaxLength(500);

        builder.Ignore(x => x.DurationMinutes);

        builder.HasIndex(x => x.IncidentCode).IsUnique();
        builder.HasIndex(x => x.OccurredAt);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.Category);
        builder.HasIndex(x => x.Severity);
    }
}
