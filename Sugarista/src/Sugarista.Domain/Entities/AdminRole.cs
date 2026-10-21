using Sugarista.Domain.Common;
using Sugarista.Domain.Constants;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

/// <summary>
/// A named security role bundling a permission set (نقش دسترسی).
/// System roles are seeded and cannot be deleted; every user assigned to a
/// role receives the union of (own permissions + role permissions) at login.
/// </summary>
public class AdminRole : BaseAuditableEntity
{
    private AdminRole()
    {
    }

    public AdminRole(string name, string displayName, bool isSystem)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Role name is required.");
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new DomainException("نام نمایشی نقش الزامی است.");
        }

        Name = name.Trim();
        DisplayName = displayName.Trim();
        IsSystem = isSystem;
    }

    /// <summary>Stable machine name (لاس‌تن: "Cashier", "InventoryManager", "Hr", ...).</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Persian label shown in the UI.</summary>
    public string DisplayName { get; private set; } = string.Empty;

    public bool IsSystem { get; private set; }

    public ICollection<AdminRolePermission> Permissions { get; private set; } = [];

    public void Update(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new DomainException("نام نمایشی نقش الزامی است.");
        }

        DisplayName = displayName.Trim();
    }

    /// <summary>Replaces the permission bundle — invalid keys are rejected.</summary>
    public void ReplacePermissions(IEnumerable<string> keys)
    {
        var normalized = keys
            .Select(key => key.Trim())
            .Where(key => key.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var invalid = normalized
            .FirstOrDefault(key => !AdminPermissions.IsValid(key));

        if (invalid is not null)
        {
            throw new DomainException($"مجوز نامعتبر: {invalid}");
        }

        Permissions.Clear();
        foreach (var key in normalized)
        {
            Permissions.Add(new AdminRolePermission(Id, key));
        }
    }
}
