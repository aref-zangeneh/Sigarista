using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sugarista.Domain.Entities;

namespace Sugarista.Infrastructure.Data.Configurations;

public class AdminRolePermissionConfiguration : IEntityTypeConfiguration<AdminRolePermission>
{
    public void Configure(EntityTypeBuilder<AdminRolePermission> builder)
    {
        // the role-permission row has no DbSet of its own — pin the plural table
        // name so it matches AdminUserPermissions convention.
        builder.ToTable("AdminRolePermissions");
    }
}
