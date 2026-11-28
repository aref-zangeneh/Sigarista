using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sugarista.Domain.Entities;

namespace Sugarista.Infrastructure.Data.Configurations;

public class CustomerDemandConfiguration : IEntityTypeConfiguration<CustomerDemand>
{
    public void Configure(EntityTypeBuilder<CustomerDemand> builder)
    {
        builder.ToTable("CustomerDemands");

        builder.Property(x => x.RequestedItem)
            .IsRequired()
            .HasMaxLength(160);

        builder.Property(x => x.NormalizedKey)
            .IsRequired()
            .HasMaxLength(160);

        builder.Property(x => x.Channel)
            .IsRequired()
            .HasMaxLength(40)
            .HasDefaultValue("POS");

        builder.Property(x => x.CustomerName)
            .HasMaxLength(120);

        builder.Property(x => x.CustomerPhone)
            .HasMaxLength(30);

        builder.Property(x => x.Notes)
            .HasMaxLength(500);

        builder.Property(x => x.RecordedByName)
            .HasMaxLength(150);

        builder.HasIndex(x => x.RequestedAt);
        builder.HasIndex(x => x.NormalizedKey);
        builder.HasIndex(x => x.Category);
        builder.HasIndex(x => x.Status);
    }
}
