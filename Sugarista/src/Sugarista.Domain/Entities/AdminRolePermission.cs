using Sugarista.Domain.Common;
using Sugarista.Domain.Constants;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

public class AdminRolePermission : BaseEntity
{
    private AdminRolePermission()
    {
    }

    public AdminRolePermission(Guid adminRoleId, string permissionKey)
    {
        if (adminRoleId == Guid.Empty)
        {
            throw new DomainException("Role is required.");
        }

        if (!AdminPermissions.IsValid(permissionKey))
        {
            throw new DomainException($"Invalid admin permission key: {permissionKey}");
        }

        AdminRoleId = adminRoleId;
        PermissionKey = permissionKey;
    }

    public Guid AdminRoleId { get; private set; }

    public AdminRole AdminRole { get; private set; } = null!;

    public string PermissionKey { get; private set; } = string.Empty;
}
