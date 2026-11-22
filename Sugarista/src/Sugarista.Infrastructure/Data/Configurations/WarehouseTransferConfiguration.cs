using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sugarista.Domain.Entities;

namespace Sugarista.Infrastructure.Data.Configurations;

public class WarehouseTransferConfiguration : IEntityTypeConfiguration<WarehouseTransfer>
{
    public void Configure(EntityTypeBuilder<WarehouseTransfer> builder)
    {
        builder.Property(t => t.ReferenceNumber)
            .HasMaxLength(WarehouseTransfer.MaxReferenceLength)
            .IsRequired();

        builder.Property(t => t.Note)
            .HasMaxLength(WarehouseTransfer.MaxNoteLength);

        builder.Property(t => t.TransferredBy)
            .HasMaxLength(WarehouseTransfer.MaxTransferredByLength);

        builder.HasIndex(t => t.ReferenceNumber).IsUnique();
        builder.HasIndex(t => t.TransferredAt);
        builder.HasIndex(t => t.SourceWarehouseId);
        builder.HasIndex(t => t.DestinationWarehouseId);

        builder.HasOne(t => t.SourceWarehouse)
            .WithMany()
            .HasForeignKey(t => t.SourceWarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.DestinationWarehouse)
            .WithMany()
            .HasForeignKey(t => t.DestinationWarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(t => t.Lines)
            .WithOne(l => l.Transfer)
            .HasForeignKey(l => l.TransferId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class WarehouseTransferLineConfiguration : IEntityTypeConfiguration<WarehouseTransferLine>
{
    public void Configure(EntityTypeBuilder<WarehouseTransferLine> builder)
    {
        builder.Property(l => l.Quantity).HasColumnType("numeric(12,3)").IsRequired();
        builder.Property(l => l.SourceQuantityBefore).HasColumnType("numeric(12,3)").IsRequired();
        builder.Property(l => l.SourceQuantityAfter).HasColumnType("numeric(12,3)").IsRequired();
        builder.Property(l => l.DestinationQuantityBefore).HasColumnType("numeric(12,3)").IsRequired();
        builder.Property(l => l.DestinationQuantityAfter).HasColumnType("numeric(12,3)").IsRequired();

        builder.HasIndex(l => new { l.TransferId, l.IngredientId });

        builder.HasOne(l => l.Ingredient)
            .WithMany()
            .HasForeignKey(l => l.IngredientId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
