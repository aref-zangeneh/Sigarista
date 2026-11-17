using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sugarista.Domain.Entities;

namespace Sugarista.Infrastructure.Data.Configurations;

public class WarehouseConfiguration : IEntityTypeConfiguration<Warehouse>
{
    public void Configure(EntityTypeBuilder<Warehouse> builder)
    {
        builder.Property(w => w.Name)
            .HasMaxLength(Warehouse.MaxNameLength)
            .IsRequired();

        builder.Property(w => w.Code)
            .HasMaxLength(Warehouse.MaxCodeLength);

        builder.Property(w => w.Description)
            .HasMaxLength(Warehouse.MaxDescriptionLength);

        // انبار → انبارک (one nesting level). A parent cannot be deleted while
        // sub-warehouses still reference it (Restrict).
        builder.HasOne(w => w.Parent)
            .WithMany()
            .HasForeignKey(w => w.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(w => w.ParentId);
        builder.HasIndex(w => w.IsActive);
    }
}
