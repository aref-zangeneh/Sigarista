using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

/// <summary>
/// Admin-defined warehouse (انبار) or sub-warehouse (انبارک).
/// <see cref="ParentId"/> is null for a top-level warehouse; when set the row
/// is a sub-warehouse nested under its parent (one level deep is allowed —
/// sub-warehouses cannot be parents, so the hierarchy stays انبار → انبارک).
/// Existing stock rows stay linked to no warehouse (the "main" stock); only
/// rows explicitly linked via WarehouseId belong to a warehouse.
/// </summary>
public class Warehouse : BaseAuditableEntity
{
    public const int MaxNameLength = 120;
    public const int MaxCodeLength = 40;
    public const int MaxDescriptionLength = 500;

    public Warehouse(
        string name,
        string? code = null,
        string? description = null,
        Guid? parentId = null,
        int displayOrder = 0,
        bool isActive = true)
    {
        Update(name, code, description, displayOrder, isActive);
        ParentId = ValidateParentId(parentId);
    }

    private Warehouse()
    {
    }

    public string Name { get; private set; } = string.Empty;

    public string? Code { get; private set; }

    public string? Description { get; private set; }

    /// <summary>Null → top-level warehouse (انبار); set → sub-warehouse (انبارک).</summary>
    public Guid? ParentId { get; private set; }

    public Warehouse? Parent { get; private set; }

    public bool IsActive { get; private set; }

    public int DisplayOrder { get; private set; }

    /// <summary>True when this row is a انبارک under a parent warehouse.</summary>
    public bool IsSubWarehouse => ParentId is not null;

    public void Update(
        string name,
        string? code,
        string? description,
        int displayOrder,
        bool isActive)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Warehouse name is required.");
        }

        if (name.Trim().Length > MaxNameLength)
        {
            throw new DomainException($"Warehouse name must be at most {MaxNameLength} characters.");
        }

        if (code is { Length: > MaxCodeLength })
        {
            throw new DomainException($"Warehouse code must be at most {MaxCodeLength} characters.");
        }

        if (description is { Length: > MaxDescriptionLength })
        {
            throw new DomainException($"Warehouse description must be at most {MaxDescriptionLength} characters.");
        }

        if (displayOrder < 0)
        {
            throw new DomainException("Warehouse display order must not be negative.");
        }

        Name = name.Trim();
        Code = string.IsNullOrWhiteSpace(code) ? null : code.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        DisplayOrder = displayOrder;
        IsActive = isActive;
    }

    /// <summary>
    /// Moves the warehouse under a new parent (or to the top level when null).
    /// Only called after the caller has verified the target parent exists,
    /// is a top-level warehouse, and is not this row itself.
    /// </summary>
    public void MoveTo(Guid? parentId)
    {
        ParentId = ValidateParentId(parentId);
    }

    private static Guid? ValidateParentId(Guid? parentId)
    {
        if (parentId is { } value && value == Guid.Empty)
        {
            throw new DomainException("Warehouse parent is invalid.");
        }

        return parentId;
    }
}
