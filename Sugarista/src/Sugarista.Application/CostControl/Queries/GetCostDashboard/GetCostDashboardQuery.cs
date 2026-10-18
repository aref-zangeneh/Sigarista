using System;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.CostControl.Queries.GetCostDashboard;

public record GetCostDashboardQuery : IRequest<CostDashboardBundleDto>;

public record CostDashboardBundleDto(
    string? SavedState,
    long SavedAt,
    IReadOnlyDictionary<string, string> Meta,
    bool StateExists,
    IReadOnlyCollection<CostControlIngredientDto> Ingredients,
    IReadOnlyCollection<CostControlMenuItemDto> MenuItems);

public record CostControlIngredientDto(
    Guid Id,
    string Name,
    string EnglishName,
    string Unit,
    decimal UnitCost,
    int DisplayOrder);

public record CostControlRecipeLineDto(
    Guid IngredientId,
    decimal Quantity,
    string? PreparationNote);

public record CostControlMenuItemDto(
    Guid Id,
    Guid CategoryId,
    string PersianName,
    string EnglishName,
    string Category,
    string CategorySlug,
    bool IsActive,
    bool IsOrderable,
    bool Featured,
    decimal TaxPercent,
    int? PreparingDurationMinutes,
    decimal DefaultPrice,
    decimal? SpecialOfferPrice,
    decimal? SmallPrice,
    decimal? MediumPrice,
    decimal? LargePrice,
    IReadOnlyList<CostControlRecipeLineDto> Recipe,
    int SoldLast30,
    int SoldTotal,
    double DailyAvg,
    DateTimeOffset? LastSoldAt);

public class GetCostDashboardQueryHandler : IRequestHandler<GetCostDashboardQuery, CostDashboardBundleDto>
{
    private readonly IApplicationDbContext _dbContext;

    public GetCostDashboardQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<CostDashboardBundleDto> Handle(GetCostDashboardQuery request, CancellationToken cancellationToken)
    {
        var ingredients = await _dbContext.Ingredients
            .AsNoTracking()
            .Where(ingredient => ingredient.IsActive)
            .OrderBy(ingredient => ingredient.DisplayOrder)
            .ThenBy(ingredient => ingredient.PersianName)
            .Select(ingredient => new CostControlIngredientDto(
                ingredient.Id,
                ingredient.PersianName,
                ingredient.EnglishName,
                ingredient.Unit,
                ingredient.UnitCost,
                ingredient.DisplayOrder))
            .ToListAsync(cancellationToken);

        var recipes = await _dbContext.RecipeItems
            .AsNoTracking()
            .OrderBy(recipe => recipe.Id)
            .Select(recipe => new
            {
                recipe.MenuItemId,
                IngredientId = recipe.IngredientId,
                recipe.Quantity,
                recipe.PreparationNote
            })
            .ToListAsync(cancellationToken);

        var recipeByItem = recipes
            .GroupBy(recipe => recipe.MenuItemId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<CostControlRecipeLineDto>)group
                    .Select(recipe => new CostControlRecipeLineDto(recipe.IngredientId, recipe.Quantity, recipe.PreparationNote))
                    .ToList());

        var menuItems = await _dbContext.MenuItems
            .AsNoTracking()
            .Include(item => item.Category)
            .OrderBy(item => item.DisplayOrder)
            .ThenBy(item => item.PersianName)
            .Select(item => new
            {
                item.Id,
                item.CategoryId,
                item.PersianName,
                item.EnglishName,
                Category = item.Category.Name,
                CategorySlug = item.Category.Slug,
                item.IsActive,
                item.IsOrderable,
                item.Featured,
                item.TaxPercent,
                item.PreparingDurationMinutes,
                item.SpecialOfferPrice,
                item.SmallPrice,
                item.MediumPrice,
                item.LargePrice
            })
            .ToListAsync(cancellationToken);

        var paidOrders = _dbContext.Orders
            .AsNoTracking()
            .Where(order => order.Status == OrderStatus.Paid);

        var salesLast30 = await paidOrders
            .Where(order => order.CreatedAt >= DateTimeOffset.UtcNow.AddDays(-30))
            .SelectMany(order => order.Lines)
            .GroupBy(line => line.MenuItemId)
            .Select(group => new { MenuItemId = group.Key, Quantity = group.Sum(line => line.Quantity), Last = group.Max(line => line.Order.CreatedAt) })
            .ToListAsync(cancellationToken);

        var salesTotal = await paidOrders
            .SelectMany(order => order.Lines)
            .GroupBy(line => line.MenuItemId)
            .Select(group => new { MenuItemId = group.Key, Quantity = group.Sum(line => line.Quantity), Last = group.Max(line => line.Order.CreatedAt) })
            .ToListAsync(cancellationToken);

        var firstPaidAt = await paidOrders
            .MinAsync(order => (DateTimeOffset?)order.CreatedAt, cancellationToken);

        var last30 = salesLast30.ToDictionary(sale => sale.MenuItemId);
        var totalSales = salesTotal.ToDictionary(sale => sale.MenuItemId);
        var spanDays = firstPaidAt.HasValue
            ? Math.Max(30d, (DateTimeOffset.UtcNow - firstPaidAt.Value).TotalDays + 1)
            : 30d;

        var items = menuItems.Select(item =>
        {
            recipeByItem.TryGetValue(item.Id, out var recipe);
            var last30sale = last30.TryGetValue(item.Id, out var recent) ? recent : null;
            var totalSale = totalSales.TryGetValue(item.Id, out var total) ? total : null;
            var defaultPrice = item.MediumPrice ?? item.SmallPrice ?? item.LargePrice ?? item.SpecialOfferPrice ?? 0m;

            return new CostControlMenuItemDto(
                item.Id,
                item.CategoryId,
                item.PersianName,
                item.EnglishName,
                item.Category,
                item.CategorySlug,
                item.IsActive,
                item.IsOrderable,
                item.Featured,
                item.TaxPercent,
                item.PreparingDurationMinutes,
                defaultPrice,
                item.SpecialOfferPrice,
                item.SmallPrice,
                item.MediumPrice,
                item.LargePrice,
                recipe ?? [],
                last30sale?.Quantity ?? 0,
                totalSale?.Quantity ?? 0,
                spanDays > 0 ? Math.Round((totalSale?.Quantity ?? 0) / spanDays, 1) : 0,
                last30sale?.Last ?? totalSale?.Last);
        }).ToList();

        var board = await _dbContext.CostControlBoards
            .AsNoTracking()
            .OrderBy(board => board.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var meta = new Dictionary<string, string>
        {
            ["savedVersion"] = board?.Version.ToString() ?? string.Empty,
        };

        return new CostDashboardBundleDto(
            board?.Json,
            board?.LastModifiedAt?.ToUnixTimeMilliseconds() ?? 0,
            meta,
            board is not null,
            ingredients,
            items);
    }
}
