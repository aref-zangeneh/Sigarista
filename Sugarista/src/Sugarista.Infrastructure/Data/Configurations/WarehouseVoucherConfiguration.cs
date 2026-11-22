using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sugarista.Domain.Entities;

namespace Sugarista.Infrastructure.Data.Configurations;

public class WarehouseVoucherConfiguration : IEntityTypeConfiguration<WarehouseVoucher>
{
    public void Configure(EntityTypeBuilder<WarehouseVoucher> builder)
    {
        builder.Property(v => v.VoucherNumber)
            .HasMaxLength(WarehouseVoucher.MaxVoucherNumberLength)
            .IsRequired();

        builder.Property(v => v.VoucherType)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(v => v.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(WarehouseVoucherStatus.Posted)
            .IsRequired();

        builder.Property(v => v.Reason)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(v => v.CounterpartyName)
            .HasMaxLength(WarehouseVoucher.MaxCounterpartyLength);

        builder.Property(v => v.ExternalReference)
            .HasMaxLength(WarehouseVoucher.MaxExternalReferenceLength);

        builder.Property(v => v.TotalAmount)
            .HasColumnType("numeric(18,2)")
            .HasDefaultValue(0m)
            .IsRequired();

        builder.Property(v => v.Note)
            .HasMaxLength(WarehouseVoucher.MaxNoteLength);

        builder.Property(v => v.CreatedByName)
            .HasMaxLength(WarehouseVoucher.MaxActorNameLength);

        builder.Property(v => v.CancelledByName)
            .HasMaxLength(WarehouseVoucher.MaxActorNameLength);

        builder.Property(v => v.CancellationReason)
            .HasMaxLength(WarehouseVoucher.MaxCancellationReasonLength);

        builder.HasIndex(v => v.VoucherNumber).IsUnique();
        builder.HasIndex(v => v.VoucherType);
        builder.HasIndex(v => v.Status);
        builder.HasIndex(v => v.VoucherDate);
        builder.HasIndex(v => v.WarehouseId);
        builder.HasIndex(v => v.SupplierId);

        builder.HasOne(v => v.Warehouse)
            .WithMany()
            .HasForeignKey(v => v.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(v => v.Supplier)
            .WithMany()
            .HasForeignKey(v => v.SupplierId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(v => v.Lines)
            .WithOne(l => l.Voucher)
            .HasForeignKey(l => l.VoucherId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class WarehouseVoucherLineConfiguration : IEntityTypeConfiguration<WarehouseVoucherLine>
{
    public void Configure(EntityTypeBuilder<WarehouseVoucherLine> builder)
    {
        builder.Property(l => l.Quantity).HasColumnType("numeric(12,3)").IsRequired();
        builder.Property(l => l.UnitPrice).HasColumnType("numeric(18,2)");
        builder.Property(l => l.LineTotal).HasColumnType("numeric(18,2)").HasDefaultValue(0m).IsRequired();
        builder.Property(l => l.StockBefore).HasColumnType("numeric(12,3)").IsRequired();
        builder.Property(l => l.StockAfter).HasColumnType("numeric(12,3)").IsRequired();
        builder.Property(l => l.BatchNumber).HasMaxLength(WarehouseVoucher.MaxBatchNumberLength);
        builder.Property(l => l.LineNote).HasMaxLength(WarehouseVoucher.MaxLineNoteLength);

        builder.HasIndex(l => new { l.VoucherId, l.IngredientId });
        builder.HasIndex(l => l.ExpirationDate);

        builder.HasOne(l => l.Ingredient)
            .WithMany()
            .HasForeignKey(l => l.IngredientId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
