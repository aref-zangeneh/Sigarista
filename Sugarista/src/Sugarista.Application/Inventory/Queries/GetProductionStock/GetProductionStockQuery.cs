using Microsoft.EntityFrameworkCore;

namespace Sugarista.Application.Inventory.Queries.GetProductionStock;

public record GetProductionStockQuery : IRequest<IReadOnlyCollection<ProductionStockDto>>;

public record ProductionStockDto(
    Guid MenuItemId,
    string MenuItemPersianName,
    string MenuItemEnglishName,
    decimal CurrentQuantity,
    decimal MinAlertQuantity,
    bool IsLowStock,
    DateTimeOffset LastUpdatedAt);

public class GetProductionStockQueryHandler : IRequestHandler<GetProductionStockQuery, IReadOnlyCollection<ProductionStockDto>>
{
    private readonly IApplicationDbContext _dbContext;

    public GetProductionStockQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<ProductionStockDto>> Handle(
        GetProductionStockQuery request,
        CancellationToken cancellationToken)
    {
        return await _dbContext.MenuItemStocks
            .AsNoTracking()
            .Include(s => s.MenuItem)
            .OrderByDescending(s => s.CurrentQuantity)
            .ThenBy(s => s.MenuItem.PersianName)
            .Select(s => new ProductionStockDto(
                s.MenuItemId,
                s.MenuItem.PersianName,
                s.MenuItem.EnglishName,
                s.CurrentQuantity,
                s.MinAlertQuantity,
                s.CurrentQuantity < s.MinAlertQuantity,
                s.LastUpdatedAt))
            .ToListAsync(cancellationToken);
    }
}
