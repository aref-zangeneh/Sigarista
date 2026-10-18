using Microsoft.EntityFrameworkCore;
using Sugarista.Application.MenuItems.Services;

namespace Sugarista.Application.MenuItems.Queries.GetMenuItemPricing;

public record GetMenuItemPricingQuery : IRequest<IReadOnlyCollection<MenuItemPricingDto>>;

public record MenuItemPricingDto(
    Guid Id,
    string PersianName,
    string EnglishName,
    string CategoryName,
    bool IsActive,
    bool HasRecipe,
    int RecipeLineCount,
    decimal CostPerServing,
    decimal? MarkupPercent,
    decimal? SmallPrice,
    string? SmallVolume,
    decimal SmallCostShare,
    decimal? SuggestedSmallPrice,
    decimal? MediumPrice,
    string? MediumVolume,
    decimal MediumCostShare,
    decimal? SuggestedMediumPrice,
    decimal? LargePrice,
    string? LargeVolume,
    decimal LargeCostShare,
    decimal? SuggestedLargePrice);

public class GetMenuItemPricingQueryHandler : IRequestHandler<GetMenuItemPricingQuery, IReadOnlyCollection<MenuItemPricingDto>>
{
    private readonly IApplicationDbContext _dbContext;

    public GetMenuItemPricingQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<MenuItemPricingDto>> Handle(
        GetMenuItemPricingQuery request,
        CancellationToken cancellationToken)
    {
        var items = await _dbContext.MenuItems
            .AsNoTracking()
            .Include(item => item.Category)
            .Include(item => item.RecipeItems)
            .ThenInclude(line => line.Ingredient)
            .OrderBy(item => item.DisplayOrder)
            .ThenBy(item => item.PersianName)
            .ToListAsync(cancellationToken);

        var result = new List<MenuItemPricingDto>(items.Count);

        foreach (var item in items)
        {
            var cost = MenuPricingService.ComputeCostPerServing(item);
            var factors = MenuPricingService.ComputeSizeFactors(item);
            var markup = (item.MarkupPercent ?? 0m) / 100m;

            result.Add(new MenuItemPricingDto(
                item.Id,
                item.PersianName,
                item.EnglishName,
                item.Category?.Name ?? string.Empty,
                item.IsActive,
                item.RecipeItems.Count > 0,
                item.RecipeItems.Count,
                Math.Round(cost, 2),
                item.MarkupPercent,
                item.SmallPrice,
                item.SmallVolume,
                Math.Round(factors.TryGetValue("small", out var smallFactor) ? smallFactor : 1m, 3),
                BuildSuggested(item.SmallPrice, cost, factors.TryGetValue("small", out var sf) ? sf : 1m, markup),
                item.MediumPrice,
                item.MediumVolume,
                Math.Round(factors.TryGetValue("medium", out var mediumFactor) ? mediumFactor : 1m, 3),
                BuildSuggested(item.MediumPrice, cost, factors.TryGetValue("medium", out var mf) ? mf : 1m, markup),
                item.LargePrice,
                item.LargeVolume,
                Math.Round(factors.TryGetValue("large", out var largeFactor) ? largeFactor : 1m, 3),
                BuildSuggested(item.LargePrice, cost, factors.TryGetValue("large", out var lf2) ? lf2 : 1m, markup)));
        }

        return result;
    }

    private static decimal? BuildSuggested(decimal? existingPrice, decimal cost, decimal factor, decimal markup)
    {
        if (!existingPrice.HasValue || cost <= 0)
        {
            return null;
        }

        var value = cost * factor * (1m + markup);
        if (value <= 0)
        {
            return existingPrice;
        }

        return MenuPricingService.RoundFinalPrice(value);
    }
}
