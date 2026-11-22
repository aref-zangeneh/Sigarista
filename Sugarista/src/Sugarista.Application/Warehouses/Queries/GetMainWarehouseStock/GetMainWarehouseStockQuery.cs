using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.Warehouses.Commands.TransferWarehouseStock;
using Sugarista.Application.Warehouses.Dto;
using Sugarista.Application.Warehouses.Queries.GetWarehouseStock;

namespace Sugarista.Application.Warehouses.Queries.GetMainWarehouseStock;

/// <summary>
/// Returns all main-inventory (<see cref="Domain.Entities.InventoryStock"/>) rows
/// in the same DTO shape as <see cref="GetWarehouseStockQuery"/> so inter-warehouse
/// transfers can pick Main Inventory as source or destination seamlessly.
/// </summary>
public record GetMainWarehouseStockQuery : IRequest<WarehouseStockResultDto>;

public class GetMainWarehouseStockQueryHandler
    : IRequestHandler<GetMainWarehouseStockQuery, WarehouseStockResultDto>
{
    private readonly IApplicationDbContext _dbContext;

    public GetMainWarehouseStockQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<WarehouseStockResultDto> Handle(
        GetMainWarehouseStockQuery request,
        CancellationToken cancellationToken)
    {
        var items = await _dbContext.InventoryStocks
            .AsNoTracking()
            .OrderBy(s => s.Ingredient.PersianName)
            .Select(s => new WarehouseStockItemDto(
                s.IngredientId,
                s.Ingredient.PersianName,
                s.Ingredient.EnglishName,
                s.Ingredient.Unit,
                s.CurrentQuantity,
                s.MinAlertQuantity,
                s.CurrentQuantity < s.MinAlertQuantity,
                s.LastUpdatedAt))
            .ToListAsync(cancellationToken);

        return new WarehouseStockResultDto(
            Guid.Empty,
            TransferWarehouseStockCommandHandler.MainWarehouseDisplayName,
            true,
            items);
    }
}
