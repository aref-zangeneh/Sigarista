using Sugarista.Application.Common.Interfaces;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Warehouses.Hierarchy;

/// <summary>
/// Pure hierarchy rules for انبار → انبارک, kept free of data access so they
/// are unit-testable: a sub-warehouse can never adopt children and a
/// warehouse can never be moved under itself.
/// </summary>
public static class WarehouseHierarchyGuard
{
    /// <summary>
    /// True when <paramref name="candidateParentId"/> is a valid parent for
    /// <paramref name="warehouseId"/>: it must differ from the warehouse and
    /// the candidate itself must be a top-level warehouse (not a انبارک).
    /// </summary>
    public static bool CanAdopt(
        IEnumerable<(Guid Id, Guid? ParentId)> allWarehouses,
        Guid warehouseId,
        Guid? candidateParentId)
    {
        if (candidateParentId is not { } parentId)
        {
            return true; // moving to the top level is always allowed
        }

        if (parentId == warehouseId)
        {
            return false; // cannot parent itself
        }

        // Only top-level warehouses (انبار) may adopt children (انبارک).
        return allWarehouses.All(w => w.Id != parentId || w.ParentId is null);
    }
}
