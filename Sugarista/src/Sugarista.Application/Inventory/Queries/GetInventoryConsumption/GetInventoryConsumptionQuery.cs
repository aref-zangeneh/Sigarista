using Microsoft.EntityFrameworkCore;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Inventory.Queries.GetInventoryConsumption;

public record GetInventoryConsumptionQuery(int Days = 30) : IRequest<IReadOnlyCollection<IngredientConsumptionDto>>;

public record IngredientConsumptionDto(
    Guid IngredientId,
    string PersianName,
    string EnglishName,
    string Unit,
    string Kind,
    string InventoryLocation,
    decimal CurrentQuantity,
    decimal MinAlertQuantity,
    decimal ConsumedQuantity,
    decimal DailyAvg,
    decimal? DaysRemaining,
    decimal SuggestedOrderQuantity,
    int PurchasesInWindow,
    DateTimeOffset? LastConsumedAt,
    string Status);

public class GetInventoryConsumptionQueryHandler
    : IRequestHandler<GetInventoryConsumptionQuery, IReadOnlyCollection<IngredientConsumptionDto>>
{
    private readonly IApplicationDbContext _dbContext;

    public GetInventoryConsumptionQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<IngredientConsumptionDto>> Handle(
        GetInventoryConsumptionQuery request,
        CancellationToken cancellationToken)
    {
        var days = request.Days switch
        {
            < 1 => 1,
            > 180 => 180,
            _ => request.Days,
        };

        var cutoff = DateTimeOffset.UtcNow.AddDays(-days);
        var consumptionHorizon = days + 7;

        var stocks = await _dbContext.InventoryStocks
            .AsNoTracking()
            .Include(s => s.Ingredient)
            .ToListAsync(cancellationToken);

        var consumedByIngredient = await _dbContext.StockMovements
            .AsNoTracking()
            .Where(m => m.OccurredAt >= cutoff && m.Quantity < 0)
            .GroupBy(m => m.IngredientId)
            .Select(g => new
            {
                IngredientId = g.Key,
                Consumed = g.Sum(m => -m.Quantity),
                LastConsumedAt = g.Max(m => m.OccurredAt),
            })
            .ToDictionaryAsync(x => x.IngredientId, x => x, cancellationToken);

        var purchasesByIngredient = await _dbContext.InventoryPurchases
            .AsNoTracking()
            .Where(p => p.ReceivedAt >= cutoff)
            .GroupBy(p => p.IngredientId)
            .Select(g => new
            {
                IngredientId = g.Key,
                Count = g.Count(),
            })
            .ToDictionaryAsync(x => x.IngredientId, x => x.Count, cancellationToken);

        var items = new List<IngredientConsumptionDto>(stocks.Count);

        foreach (var stock in stocks)
        {
            var hasConsumption = consumedByIngredient.TryGetValue(stock.IngredientId, out var consumption);
            var consumed = hasConsumption ? consumption!.Consumed : 0m;
            var dailyAvg = consumed / days;
            var daysRemaining = dailyAvg > 0 ? stock.CurrentQuantity / dailyAvg : (decimal?)null;

            purchasesByIngredient.TryGetValue(stock.IngredientId, out var purchaseCount);
            var status = ResolveStatus(stock, dailyAvg, daysRemaining);

            // Suggested order: cover the next reorder horizon, minus what is already in stock.
            var suggested = 0m;
            if (hasConsumption && dailyAvg > 0)
            {
                suggested = (dailyAvg * consumptionHorizon) - stock.CurrentQuantity;
                if (suggested < 0)
                {
                    suggested = 0m;
                }
            }

            items.Add(new IngredientConsumptionDto(
                stock.IngredientId,
                stock.Ingredient.PersianName,
                stock.Ingredient.EnglishName,
                stock.Ingredient.Unit,
                stock.Ingredient.Kind,
                stock.Ingredient.InventoryLocation,
                stock.CurrentQuantity,
                stock.MinAlertQuantity,
                consumed,
                Math.Round(dailyAvg, 3),
                daysRemaining is { } dr ? Math.Round(dr, 1) : null,
                Math.Round(suggested, 3),
                purchaseCount,                hasConsumption ? consumption!.LastConsumedAt : null,
                status));
        }

        return items
            .OrderBy(i => i.Status == "ok" ? 1 : 0)
            .ThenByDescending(i => i.DailyAvg)
            .ThenBy(i => i.PersianName)
            .ToList();
    }

    private static string ResolveStatus(InventoryStock stock, decimal dailyAvg, decimal? daysRemaining)
    {
        if (stock.CurrentQuantity <= 0)
        {
            return "out";
        }

        if (dailyAvg > 0 && daysRemaining is { } remaining && remaining <= 3)
        {
            return "critical";
        }

        if (stock.CurrentQuantity < stock.MinAlertQuantity)
        {
            return "low";
        }

        if (dailyAvg > 0 && daysRemaining is { } left && left <= 7)
        {
            return "watch";
        }

        return "ok";
    }
}
