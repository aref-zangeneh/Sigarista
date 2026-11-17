using MediatR;
using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.Warehouses.Dto;

namespace Sugarista.Application.Warehouses.Queries.GetWarehouseStock;

/// <summary>Stock rows of one warehouse (or انبارک) joined with their ingredients.</summary>
public record GetWarehouseStockQuery(Guid WarehouseId) : IRequest<WarehouseStockResultDto>;

public record WarehouseStockResultDto(
    Guid WarehouseId,
    string WarehouseName,
    bool WarehouseIsActive,
    IReadOnlyCollection<WarehouseStockItemDto> Items);

public class GetWarehouseStockQueryHandler
    : IRequestHandler<GetWarehouseStockQuery, WarehouseStockResultDto>
{
    private readonly IApplicationDbContext _dbContext;

    public GetWarehouseStockQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<WarehouseStockResultDto> Handle(
        GetWarehouseStockQuery request,
        CancellationToken cancellationToken)
    {
        var warehouse = await _dbContext.Warehouses
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == request.WarehouseId, cancellationToken)
            ?? throw new KeyNotFoundException("Warehouse was not found.");

        var items = await _dbContext.WarehouseStocks
            .AsNoTracking()
            .Where(s => s.WarehouseId == request.WarehouseId)
            .OrderBy(s => s.Ingredient.PersianName)
            .Select(s => new WarehouseStockItemDto(
                s.IngredientId,
                s.Ingredient.PersianName,
                s.Ingredient.EnglishName,
                s.Ingredient.Unit,
                s.Quantity,
                s.MinAlertQuantity,
                s.Quantity < s.MinAlertQuantity,
                s.LastUpdatedAt))
            .ToListAsync(cancellationToken);

        return new WarehouseStockResultDto(
            warehouse.Id,
            warehouse.Name,
            warehouse.IsActive,
            items);
    }
}
