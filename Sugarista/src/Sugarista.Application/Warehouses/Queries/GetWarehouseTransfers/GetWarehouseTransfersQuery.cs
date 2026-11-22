using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.Warehouses.Commands.TransferWarehouseStock;
using Sugarista.Application.Warehouses.Dto;

namespace Sugarista.Application.Warehouses.Queries.GetWarehouseTransfers;

public record GetWarehouseTransfersQuery(
    Guid? WarehouseId = null,
    bool MainOnly = false,
    int Limit = 50) : IRequest<IReadOnlyCollection<WarehouseTransferDto>>;

public class GetWarehouseTransfersQueryHandler
    : IRequestHandler<GetWarehouseTransfersQuery, IReadOnlyCollection<WarehouseTransferDto>>
{
    private readonly IApplicationDbContext _dbContext;

    public GetWarehouseTransfersQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<WarehouseTransferDto>> Handle(
        GetWarehouseTransfersQuery request,
        CancellationToken cancellationToken)
    {
        var limit = Math.Clamp(request.Limit, 1, 200);

        var query = _dbContext.WarehouseTransfers
            .AsNoTracking()
            .Include(t => t.SourceWarehouse)
            .Include(t => t.DestinationWarehouse)
            .Include(t => t.Lines)
            .ThenInclude(l => l.Ingredient)
            .AsQueryable();

        if (request.MainOnly)
        {
            query = query.Where(t => t.SourceWarehouseId == null || t.DestinationWarehouseId == null);
        }
        else if (request.WarehouseId is { } warehouseId)
        {
            query = query.Where(t => t.SourceWarehouseId == warehouseId || t.DestinationWarehouseId == warehouseId);
        }

        var transfers = await query
            .OrderByDescending(t => t.TransferredAt)
            .ThenByDescending(t => t.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return transfers
            .Select(t => new WarehouseTransferDto(
                t.Id,
                t.ReferenceNumber,
                t.SourceWarehouseId,
                t.SourceWarehouse?.Name ?? TransferWarehouseStockCommandHandler.MainWarehouseDisplayName,
                t.DestinationWarehouseId,
                t.DestinationWarehouse?.Name ?? TransferWarehouseStockCommandHandler.MainWarehouseDisplayName,
                t.Note,
                t.TransferredBy,
                t.TransferredAt,
                t.Lines
                    .OrderBy(l => l.Ingredient.PersianName)
                    .Select(l => new WarehouseTransferLineDto(
                        l.Id,
                        l.IngredientId,
                        l.Ingredient.PersianName,
                        l.Ingredient.EnglishName,
                        l.Ingredient.Unit,
                        l.Quantity,
                        l.SourceQuantityBefore,
                        l.SourceQuantityAfter,
                        l.DestinationQuantityBefore,
                        l.DestinationQuantityAfter))
                    .ToList()))
            .ToList();
    }
}
