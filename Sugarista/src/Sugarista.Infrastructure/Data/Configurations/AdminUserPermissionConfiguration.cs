using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sugarista.Domain.Entities;

namespace Sugarista.Infrastructure.Data.Configurations;

public class AdminUserPermissionConfiguration : IEntityTypeConfiguration<AdminUserPermission>
{
    public void Configure(EntityTypeBuilder<AdminUserPermission> builder)
    {
        builder.Property(permission => permission.PermissionKey)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(permission => new { permission.AdminUserId, permission.PermissionKey })
            .IsUnique();

        builder.HasOne(permission => permission.AdminUser)
            .WithMany(user => user.Permissions)
            .HasForeignKey(permission => permission.AdminUserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
