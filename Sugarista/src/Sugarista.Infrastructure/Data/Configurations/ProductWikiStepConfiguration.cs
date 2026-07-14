using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sugarista.Domain.Entities;

namespace Sugarista.Infrastructure.Data.Configurations;

public class ProductWikiStepConfiguration : IEntityTypeConfiguration<ProductWikiStep>
{
    public void Configure(EntityTypeBuilder<ProductWikiStep> builder)
    {
        builder.Property(s => s.Title).HasMaxLength(200).IsRequired();
        builder.Property(s => s.Instructions).HasMaxLength(4000).IsRequired();

        builder.HasOne(s => s.MenuItem)
            .WithMany()
            .HasForeignKey(s => s.MenuItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => new { s.MenuItemId, s.StepOrder }).IsUnique();
    }
}
