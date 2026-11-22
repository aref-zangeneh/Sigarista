namespace Sugarista.Application.Warehouses.Dto;

public record WarehouseTransferLineDto(
    Guid Id,
    Guid IngredientId,
    string IngredientPersianName,
    string IngredientEnglishName,
    string Unit,
    decimal Quantity,
    decimal SourceQuantityBefore,
    decimal SourceQuantityAfter,
    decimal DestinationQuantityBefore,
    decimal DestinationQuantityAfter);

public record WarehouseTransferDto(
    Guid Id,
    string ReferenceNumber,
    Guid? SourceWarehouseId,
    string SourceWarehouseName,
    Guid? DestinationWarehouseId,
    string DestinationWarehouseName,
    string? Note,
    string? TransferredBy,
    DateTimeOffset TransferredAt,
    IReadOnlyCollection<WarehouseTransferLineDto> Lines);
