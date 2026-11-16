using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sugarista.Domain.Entities;

namespace Sugarista.Infrastructure.Data.Configurations;

public class AdminTooltipConfiguration : IEntityTypeConfiguration<AdminTooltip>
{
    public void Configure(EntityTypeBuilder<AdminTooltip> builder)
    {
        builder.Property(tooltip => tooltip.Key)
            .HasMaxLength(150);

        builder.Property(tooltip => tooltip.Text)
            .HasMaxLength(1000);

        builder.HasIndex(tooltip => tooltip.Key)
            .IsUnique();
    }
}
