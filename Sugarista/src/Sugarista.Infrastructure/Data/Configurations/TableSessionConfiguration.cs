using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sugarista.Domain.Entities;

namespace Sugarista.Infrastructure.Data.Configurations;

public class TableSessionConfiguration : IEntityTypeConfiguration<TableSession>
{
    public void Configure(EntityTypeBuilder<TableSession> builder)
    {
        builder.Property(session => session.Note)
            .HasMaxLength(500);

        builder.HasIndex(session => new { session.TableId, session.EndedAt });
    }
}
