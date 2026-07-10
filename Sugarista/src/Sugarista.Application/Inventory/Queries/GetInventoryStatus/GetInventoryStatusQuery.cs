using Microsoft.EntityFrameworkCore;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Inventory.Queries.GetInventoryStatus;

public record GetInventoryStatusQuery : IRequest<InventoryStatusDto>;

public record InventoryLocationGroupDto(
    string Location,
    string LocationLabel,
    IReadOnlyCollection<IngredientStockDto> Items);

public record IngredientStockDto(
    Guid IngredientId,
    string PersianName,
    string EnglishName,
    string Unit,
    string InventoryLocation,
    decimal CurrentQuantity,
    decimal MinAlertQuantity,
    bool IsLowStock,
    DateTimeOffset LastUpdatedAt);

public record InventoryStatusDto(
    IReadOnlyCollection<InventoryLocationGroupDto> Groups,
    int TotalIngredients,
    int LowStockCount);

public class GetInventoryStatusQueryHandler : IRequestHandler<GetInventoryStatusQuery, InventoryStatusDto>
{
    private static readonly IReadOnlyDictionary<string, string> LocationLabels = new Dictionary<string, string>
    {
        ["main"] = "انبار اصلی",
        ["bar"] = "بار",
        ["kitchen"] = "آشپزخانه",
        ["dry"] = "انبار خشک",
        ["refrigerator"] = "یخچال",
    };

    private readonly IApplicationDbContext _dbContext;

    public GetInventoryStatusQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<InventoryStatusDto> Handle(GetInventoryStatusQuery request, CancellationToken cancellationToken)
    {
        var rows = await _dbContext.InventoryStocks
            .AsNoTracking()
            .Include(s => s.Ingredient)
            .OrderBy(s => s.Ingredient.DisplayOrder)
            .ToListAsync(cancellationToken);

        var groups = Ingredient.ValidLocations
            .Select(location => new InventoryLocationGroupDto(
                location,
                LocationLabels.TryGetValue(location, out var label) ? label : location,
                rows
                    .Where(s => s.Ingredient.InventoryLocation == location)
                    .Select(s => new IngredientStockDto(
                        s.IngredientId,
                        s.Ingredient.PersianName,
                        s.Ingredient.EnglishName,
                        s.Ingredient.Unit,
                        s.Ingredient.InventoryLocation,
                        s.CurrentQuantity,
                        s.MinAlertQuantity,
                        s.CurrentQuantity < s.MinAlertQuantity,
                        s.LastUpdatedAt))
                    .ToList()))
            .Where(g => g.Items.Count > 0)
            .ToList();

        var lowStockCount = rows.Count(s => s.CurrentQuantity < s.MinAlertQuantity);

        return new InventoryStatusDto(groups, rows.Count, lowStockCount);
    }
}
