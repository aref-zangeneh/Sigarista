using Microsoft.EntityFrameworkCore;

namespace Sugarista.Application.Inventory.Queries.GetStockHistory;

public record GetStockHistoryQuery(Guid IngredientId, int PageSize = 50) : IRequest<IReadOnlyCollection<StockMovementDto>>;

public record StockMovementDto(
    Guid Id,
    decimal Quantity,
    string MovementType,
    string? Note,
    string? SourceMenuItemName,
    DateTimeOffset OccurredAt);

public class GetStockHistoryQueryHandler : IRequestHandler<GetStockHistoryQuery, IReadOnlyCollection<StockMovementDto>>
{
    private readonly IApplicationDbContext _dbContext;

    public GetStockHistoryQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<StockMovementDto>> Handle(GetStockHistoryQuery request, CancellationToken cancellationToken)
    {
        var ingredientExists = await _dbContext.Ingredients
            .AnyAsync(i => i.Id == request.IngredientId, cancellationToken);

        if (!ingredientExists)
        {
            throw new KeyNotFoundException("Ingredient was not found.");
        }

        return await _dbContext.StockMovements
            .AsNoTracking()
            .Where(m => m.IngredientId == request.IngredientId)
            .OrderByDescending(m => m.OccurredAt)
            .Take(request.PageSize)
            .Select(m => new StockMovementDto(
                m.Id,
                m.Quantity,
                m.MovementType.ToString(),
                m.Note,
                m.SourceMenuItemName,
                m.OccurredAt))
            .ToListAsync(cancellationToken);
    }
}
