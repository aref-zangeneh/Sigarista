using Microsoft.EntityFrameworkCore;

namespace Sugarista.Application.Ingredients.Queries.GetAdminIngredientOptions;

public record GetAdminIngredientOptionsQuery : IRequest<IReadOnlyCollection<AdminIngredientOptionDto>>;

public record AdminIngredientOptionDto(Guid Id, string PersianName, string EnglishName, string Unit, bool IsActive);

public class GetAdminIngredientOptionsQueryHandler : IRequestHandler<GetAdminIngredientOptionsQuery, IReadOnlyCollection<AdminIngredientOptionDto>>
{
    private readonly IApplicationDbContext _dbContext;

    public GetAdminIngredientOptionsQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<AdminIngredientOptionDto>> Handle(
        GetAdminIngredientOptionsQuery request,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Ingredients
            .AsNoTracking()
            .Where(i => i.IsActive)
            .OrderBy(i => i.DisplayOrder)
            .ThenBy(i => i.PersianName)
            .Select(i => new AdminIngredientOptionDto(i.Id, i.PersianName, i.EnglishName, i.Unit, i.IsActive))
            .ToListAsync(cancellationToken);
    }
}
