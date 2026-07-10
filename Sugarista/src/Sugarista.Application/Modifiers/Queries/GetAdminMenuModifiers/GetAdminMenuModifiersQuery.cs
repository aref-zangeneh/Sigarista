using Microsoft.EntityFrameworkCore;

namespace Sugarista.Application.Modifiers.Queries.GetAdminMenuModifiers;

public record GetAdminMenuModifiersQuery : IRequest<IReadOnlyCollection<AdminMenuModifierDto>>;

public record AdminMenuModifierDto(
    Guid Id,
    string Name,
    string PersianName,
    string EnglishName,
    int DisplayOrder,
    bool IsActive);

public class GetAdminMenuModifiersQueryHandler : IRequestHandler<GetAdminMenuModifiersQuery, IReadOnlyCollection<AdminMenuModifierDto>>
{
    private readonly IApplicationDbContext _dbContext;

    public GetAdminMenuModifiersQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<AdminMenuModifierDto>> Handle(GetAdminMenuModifiersQuery request, CancellationToken cancellationToken)
    {
        return await _dbContext.MenuModifiers
            .AsNoTracking()
            .OrderBy(modifier => modifier.DisplayOrder)
            .ThenBy(modifier => modifier.Name)
            .Select(modifier => new AdminMenuModifierDto(
                modifier.Id,
                modifier.Name,
                modifier.PersianName,
                modifier.EnglishName,
                modifier.DisplayOrder,
                modifier.IsActive))
            .ToListAsync(cancellationToken);
    }
}

