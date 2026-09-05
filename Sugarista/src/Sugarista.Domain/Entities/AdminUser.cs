using Sugarista.Domain.Common;
using Sugarista.Domain.Constants;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

public class AdminUser : BaseAuditableEntity
{
    public AdminUser()
    {
    }

    public AdminUser(string username, string displayName, string passwordHash, string role, bool isActive = true)
    {
        UpdateProfile(username, displayName, role, isActive);
        SetPasswordHash(passwordHash);
    }

    public string Username { get; private set; } = string.Empty;

    public string DisplayName { get; private set; } = string.Empty;

    public string PasswordHash { get; private set; } = string.Empty;

    public string Role { get; private set; } = RoleNames.Admin;

    public bool IsActive { get; private set; } = true;

    public bool IsSuperAdmin { get; private set; }

    public ICollection<AdminUserPermission> Permissions { get; private set; } = [];

    public void UpdateProfile(string username, string displayName, string role, bool isActive, bool isSuperAdmin = false)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new DomainException("Admin username is required.");
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new DomainException("Admin display name is required.");
        }

        Username = username.Trim().ToLowerInvariant();
        DisplayName = displayName.Trim();
        Role = string.IsNullOrWhiteSpace(role) ? RoleNames.Admin : role.Trim();
        IsActive = isActive;
        IsSuperAdmin = isSuperAdmin;
    }

    public void SetSuperAdmin(bool isSuperAdmin)
    {
        IsSuperAdmin = isSuperAdmin;
    }

    public void ReplacePermissions(IEnumerable<string> permissionKeys)
    {
        var normalizedKeys = permissionKeys
            .Where(AdminPermissions.IsValid)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        Permissions.Clear();

        foreach (var permissionKey in normalizedKeys)
        {
            Permissions.Add(new AdminUserPermission(Id, permissionKey));
        }
    }

    public void SetPasswordHash(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new DomainException("Admin password hash is required.");
        }

        PasswordHash = passwordHash;
    }
}
