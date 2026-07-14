using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sugarista.Domain.Entities;

namespace Sugarista.Infrastructure.Data.Configurations;

public class ProductionBatchConfiguration : IEntityTypeConfiguration<ProductionBatch>
{
    public void Configure(EntityTypeBuilder<ProductionBatch> builder)
    {
        builder.Property(p => p.QuantityProduced)
            .HasColumnType("numeric(12,3)")
            .IsRequired();

        builder.Property(p => p.Note)
            .HasMaxLength(1000);

        builder.HasOne(p => p.MenuItem)
            .WithMany()
            .HasForeignKey(p => p.MenuItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => p.MenuItemId);
        builder.HasIndex(p => p.ProducedAt);
    }
}
