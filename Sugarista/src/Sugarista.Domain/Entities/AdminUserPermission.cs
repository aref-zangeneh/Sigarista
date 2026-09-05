using Sugarista.Domain.Common;
using Sugarista.Domain.Constants;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

public class AdminUserPermission : BaseEntity
{
    private AdminUserPermission()
    {
    }

    public AdminUserPermission(Guid adminUserId, string permissionKey)
    {
        SetPermissionKey(permissionKey);
        AdminUserId = adminUserId;
    }

    public Guid AdminUserId { get; private set; }

    public AdminUser AdminUser { get; private set; } = null!;

    public string PermissionKey { get; private set; } = string.Empty;

    public void SetPermissionKey(string permissionKey)
    {
        if (!AdminPermissions.IsValid(permissionKey))
        {
            throw new DomainException($"Invalid admin permission key: {permissionKey}");
        }

        PermissionKey = permissionKey;
    }
}
