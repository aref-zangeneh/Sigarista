using Microsoft.EntityFrameworkCore;

namespace Sugarista.Application.Ingredients.Queries.GetAdminIngredients;

public record GetAdminIngredientsQuery : IRequest<IReadOnlyCollection<AdminIngredientDto>>;

public record AdminIngredientDto(
    Guid Id,
    string PersianName,
    string EnglishName,
    string Unit,
    string Description,
    int DisplayOrder,
    bool IsActive,
    string InventoryLocation);

public class GetAdminIngredientsQueryHandler : IRequestHandler<GetAdminIngredientsQuery, IReadOnlyCollection<AdminIngredientDto>>
{
    private readonly IApplicationDbContext _dbContext;

    public GetAdminIngredientsQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<AdminIngredientDto>> Handle(GetAdminIngredientsQuery request, CancellationToken cancellationToken)
    {
        return await _dbContext.Ingredients
            .AsNoTracking()
            .OrderBy(i => i.DisplayOrder)
            .ThenBy(i => i.Name)
            .Select(i => new AdminIngredientDto(
                i.Id,
                i.PersianName,
                i.EnglishName,
                i.Unit,
                i.Description,
                i.DisplayOrder,
                i.IsActive,
                i.InventoryLocation))
            .ToListAsync(cancellationToken);
    }
}
