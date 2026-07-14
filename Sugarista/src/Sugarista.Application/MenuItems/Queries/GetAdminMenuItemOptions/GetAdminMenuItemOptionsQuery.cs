using Microsoft.EntityFrameworkCore;

namespace Sugarista.Application.MenuItems.Queries.GetAdminMenuItemOptions;

public record GetAdminMenuItemOptionsQuery : IRequest<IReadOnlyCollection<AdminMenuItemOptionDto>>;

public record AdminMenuItemOptionDto(Guid Id, string PersianName, string EnglishName, bool IsActive);

public class GetAdminMenuItemOptionsQueryHandler : IRequestHandler<GetAdminMenuItemOptionsQuery, IReadOnlyCollection<AdminMenuItemOptionDto>>
{
    private readonly IApplicationDbContext _dbContext;

    public GetAdminMenuItemOptionsQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<AdminMenuItemOptionDto>> Handle(
        GetAdminMenuItemOptionsQuery request,
        CancellationToken cancellationToken)
    {
        return await _dbContext.MenuItems
            .AsNoTracking()
            .OrderBy(item => item.DisplayOrder)
            .ThenBy(item => item.PersianName)
            .Select(item => new AdminMenuItemOptionDto(item.Id, item.PersianName, item.EnglishName, item.IsActive))
            .ToListAsync(cancellationToken);
    }
}
