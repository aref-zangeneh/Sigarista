using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;

namespace Sugarista.Application.MenuItems.Bundles.Queries.GetMenuItemBundle;

public record GetMenuItemBundleQuery(Guid MenuItemId) : IRequest<AdminMenuItemBundleDto>;

public class GetMenuItemBundleQueryHandler : IRequestHandler<GetMenuItemBundleQuery, AdminMenuItemBundleDto>
{
    private readonly IApplicationDbContext _dbContext;

    public GetMenuItemBundleQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AdminMenuItemBundleDto> Handle(
        GetMenuItemBundleQuery request,
        CancellationToken cancellationToken)
    {
        var menuItem = await _dbContext.MenuItems
            .AsNoTracking()
            .Include(m => m.RecipeItems)
            .ThenInclude(r => r.Ingredient)
            .Include(m => m.BundleItems)
            .ThenInclude(b => b.ComponentMenuItem)
            .ThenInclude(c => c.RecipeItems)
            .ThenInclude(r => r.Ingredient)
            .FirstOrDefaultAsync(m => m.Id == request.MenuItemId, cancellationToken)
            ?? throw new KeyNotFoundException("آیتم منو یافت نشد.");

        var orderedItems = menuItem.BundleItems
            .OrderBy(b => b.DisplayOrder)
            .ThenBy(b => b.Id)
            .ToList();

        var valuation = BundlePricingService.Evaluate(menuItem, orderedItems);

        var components = orderedItems
            .Zip(valuation.Components, (entity, info) => new AdminBundleComponentDto(
                entity.Id,
                info.ComponentMenuItemId,
                info.PersianName,
                info.EnglishName,
                info.ImageUrl,
                info.SizeKey,
                info.SizeLabel,
                info.Quantity,
                info.UnitRegularPrice,
                info.LineRegularPrice,
                info.UnitCost,
                info.LineCost,
                info.IsAvailable,
                info.DisplayOrder))
            .ToArray();

        return new AdminMenuItemBundleDto(
            menuItem.Id,
            menuItem.PersianName,
            menuItem.EnglishName,
            menuItem.IsBundle,
            valuation.BundlePrice,
            valuation.RegularTotal,
            valuation.SavingsAmount,
            valuation.SavingsPercent,
            valuation.TotalCostPerServing,
            valuation.EstimatedProfit,
            valuation.EstimatedMarginPercent,
            valuation.AllComponentsAvailable,
            components);
    }
}
