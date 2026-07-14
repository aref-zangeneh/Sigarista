using Microsoft.EntityFrameworkCore;

namespace Sugarista.Application.Categories.Queries.GetAdminCategoryOptions;

public record GetAdminCategoryOptionsQuery : IRequest<IReadOnlyCollection<AdminCategoryOptionDto>>;

public record AdminCategoryOptionDto(Guid Id, string Name);

public class GetAdminCategoryOptionsQueryHandler : IRequestHandler<GetAdminCategoryOptionsQuery, IReadOnlyCollection<AdminCategoryOptionDto>>
{
    private readonly IApplicationDbContext _dbContext;

    public GetAdminCategoryOptionsQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<AdminCategoryOptionDto>> Handle(
        GetAdminCategoryOptionsQuery request,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Categories
            .AsNoTracking()
            .OrderBy(category => category.DisplayOrder)
            .ThenBy(category => category.Name)
            .Select(category => new AdminCategoryOptionDto(category.Id, category.Name))
            .ToListAsync(cancellationToken);
    }
}
