using Microsoft.EntityFrameworkCore;

namespace Sugarista.Application.Ingredients.Queries.GetAdminIngredientOptions;

public record GetAdminIngredientOptionsQuery(string? Kind = null) : IRequest<IReadOnlyCollection<AdminIngredientOptionDto>>;

public record AdminIngredientOptionDto(
    Guid Id,
    string PersianName,
    string EnglishName,
    string Unit,
    bool IsActive,
    string Kind);

public class GetAdminIngredientOptionsQueryHandler : IRequestHandler<GetAdminIngredientOptionsQuery, IReadOnlyCollection<AdminIngredientOptionDto>>
{
    private readonly IApplicationDbContext _dbContext;

    public GetAdminIngredientOptionsQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<AdminIngredientOptionDto>> Handle(GetAdminIngredientOptionsQuery request, CancellationToken cancellationToken)
    {
        var query = _dbContext.Ingredients
            .AsNoTracking()
            .Where(i => i.IsActive);

        if (!string.IsNullOrWhiteSpace(request.Kind))
        {
            var kind = request.Kind.Trim().ToLowerInvariant();
            query = query.Where(i => i.Kind == kind);
        }

        return await query
            .OrderBy(i => i.DisplayOrder)
            .ThenBy(i => i.PersianName)
            .Select(i => new AdminIngredientOptionDto(i.Id, i.PersianName, i.EnglishName, i.Unit, i.IsActive, i.Kind))
            .ToListAsync(cancellationToken);
    }
}
