using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sugarista.Domain.Entities;

namespace Sugarista.Infrastructure.Data.Configurations;

public class AdminRoleConfiguration : IEntityTypeConfiguration<AdminRole>
{
    public void Configure(EntityTypeBuilder<AdminRole> builder)
    {
        builder.Property(entity => entity.Name)
            .HasMaxLength(60)
            .IsRequired();

        builder.Property(entity => entity.DisplayName)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(entity => entity.Name)
            .IsUnique();

        builder.HasMany(entity => entity.Permissions)
            .WithOne(permission => permission.AdminRole)
            .HasForeignKey(permission => permission.AdminRoleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
