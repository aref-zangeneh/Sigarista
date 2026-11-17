using Sugarista.Domain.Entities;

namespace Sugarista.Application.Warehouses.Dto;

public sealed record WarehouseDto(
    Guid Id,
    string Name,
    string? Code,
    string? Description,
    Guid? ParentId,
    string? ParentName,
    bool IsActive,
    bool IsSubWarehouse,
    int DisplayOrder,
    int SubWarehouseCount);

public sealed record WarehouseStockItemDto(
    Guid IngredientId,
    string PersianName,
    string EnglishName,
    string Unit,
    decimal Quantity,
    decimal MinAlertQuantity,
    bool IsLowStock,
    DateTimeOffset LastUpdatedAt);

public static class WarehouseMappings
{
    public static WarehouseDto ToDto(this Warehouse warehouse, string? parentName, int subWarehouseCount)
    {
        return new WarehouseDto(
            warehouse.Id,
            warehouse.Name,
            warehouse.Code,
            warehouse.Description,
            warehouse.ParentId,
            parentName,
            warehouse.IsActive,
            warehouse.IsSubWarehouse,
            warehouse.DisplayOrder,
            subWarehouseCount);
    }
}
