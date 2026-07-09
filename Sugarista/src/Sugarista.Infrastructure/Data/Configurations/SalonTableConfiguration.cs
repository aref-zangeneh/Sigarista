using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sugarista.Domain.Entities;

namespace Sugarista.Infrastructure.Data.Configurations;

public class SalonTableConfiguration : IEntityTypeConfiguration<SalonTable>
{
    public void Configure(EntityTypeBuilder<SalonTable> builder)
    {
        builder.Property(table => table.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(table => table.Name)
            .IsUnique();

        builder.HasMany(table => table.Sessions)
            .WithOne(session => session.Table)
            .HasForeignKey(session => session.TableId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
