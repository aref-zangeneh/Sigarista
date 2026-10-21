using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sugarista.Domain.Entities;

namespace Sugarista.Infrastructure.Data.Configurations;

public class CafeOpenHourConfiguration : IEntityTypeConfiguration<CafeOpenHour>
{
    public void Configure(EntityTypeBuilder<CafeOpenHour> builder)
    {
        builder.Property(hour => hour.IsOpen)
            .IsRequired();

        builder.HasIndex(hour => hour.Weekday)
            .IsUnique();
    }
}
