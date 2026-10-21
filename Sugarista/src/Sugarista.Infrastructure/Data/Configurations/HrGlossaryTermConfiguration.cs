using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sugarista.Domain.Entities;

namespace Sugarista.Infrastructure.Data.Configurations;

public class HrGlossaryTermConfiguration : IEntityTypeConfiguration<HrGlossaryTerm>
{
    public void Configure(EntityTypeBuilder<HrGlossaryTerm> builder)
    {
        builder.Property(term => term.Key)
            .HasMaxLength(80)
            .IsRequired();

        builder.Property(term => term.Definition)
            .HasMaxLength(1200)
            .IsRequired();

        builder.HasIndex(term => term.Key)
            .IsUnique();
    }
}
