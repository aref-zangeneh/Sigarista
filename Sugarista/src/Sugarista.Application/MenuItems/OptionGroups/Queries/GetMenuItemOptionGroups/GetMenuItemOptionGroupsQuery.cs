using Microsoft.EntityFrameworkCore;
using Sugarista.Domain.Entities;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Application.MenuItems.OptionGroups.Queries.GetMenuItemOptionGroups;

public record GetMenuItemOptionGroupsQuery(Guid MenuItemId) : IRequest<MenuItemOptionGroupsDto>;

public class GetMenuItemOptionGroupsQueryHandler
    : IRequestHandler<GetMenuItemOptionGroupsQuery, MenuItemOptionGroupsDto>
{
    private readonly IApplicationDbContext _dbContext;

    public GetMenuItemOptionGroupsQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<MenuItemOptionGroupsDto> Handle(
        GetMenuItemOptionGroupsQuery request,
        CancellationToken cancellationToken)
    {
        var menuItem = await _dbContext.MenuItems
            .AsNoTracking()
            .Where(item => item.Id == request.MenuItemId)
            .Select(item => new { item.Id, item.PersianName })
            .FirstOrDefaultAsync(cancellationToken);

        if (menuItem is null)
        {
            throw new DomainException("آیتم منو یافت نشد.");
        }

        var groups = await _dbContext.MenuItemOptionGroups
            .AsNoTracking()
            .Include(group => group.Options)
            .ThenInclude(option => option.Ingredients)
            .Where(group => group.MenuItemId == request.MenuItemId)
            .OrderBy(group => group.DisplayOrder)
            .ToListAsync(cancellationToken);

        return new MenuItemOptionGroupsDto(
            request.MenuItemId,
            menuItem.PersianName,
            groups
                .Select(group => new AdminMenuItemOptionGroupDto(
                    group.Id,
                    group.PersianName,
                    group.EnglishName,
                    group.IsRequired,
                    group.SelectionMode,
                    group.MinSelections,
                    group.MaxSelections,
                    group.DisplayOrder,
                    group.IsActive,
                    group.Options
                        .OrderBy(option => option.DisplayOrder)
                        .ThenBy(option => option.PersianName, StringComparer.Ordinal)
                        .Select(option => new AdminMenuItemOptionDto(
                            option.Id,
                            option.PersianName,
                            option.EnglishName,
                            option.PriceDelta,
                            option.IsDefault,
                            option.IsActive,
                            option.DisplayOrder,
                            option.Ingredients
                                .Select(ingredientRow => new AdminMenuItemOptionIngredientDto(
                                    ingredientRow.IngredientId,
                                    ingredientRow.Quantity))
                                .ToList()))
                        .ToList()))
                .ToList());
    }
}
