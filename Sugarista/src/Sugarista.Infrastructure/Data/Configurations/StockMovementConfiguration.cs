using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sugarista.Domain.Entities;

namespace Sugarista.Infrastructure.Data.Configurations;

public class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.Property(m => m.Quantity)
            .HasColumnType("numeric(12,3)")
            .IsRequired();

        builder.Property(m => m.MovementType)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(m => m.Note)
            .HasMaxLength(500);

        builder.Property(m => m.SourceMenuItemName)
            .HasMaxLength(200);

        builder.HasIndex(m => m.OrderId);
        builder.HasIndex(m => m.OrderLineId);
        builder.HasIndex(m => m.ProductionBatchId);

        builder.HasOne(m => m.Stock)
            .WithMany(s => s.Movements)
            .HasForeignKey(m => m.IngredientId)
            .HasPrincipalKey(s => s.IngredientId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(m => new { m.IngredientId, m.OccurredAt });
    }
}
