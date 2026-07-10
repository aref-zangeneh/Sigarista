using Microsoft.EntityFrameworkCore;

namespace Sugarista.Application.Addons.Queries.GetAdminAddonItems;

public record GetAdminAddonItemsQuery : IRequest<IReadOnlyCollection<AdminAddonItemDto>>;

public record AdminAddonItemDto(
    Guid Id,
    string Name,
    string PersianName,
    string EnglishName,
    string Description,
    int Price,
    IReadOnlyCollection<string> Tags,
    int DisplayOrder,
    bool IsActive);

public class GetAdminAddonItemsQueryHandler : IRequestHandler<GetAdminAddonItemsQuery, IReadOnlyCollection<AdminAddonItemDto>>
{
    private readonly IApplicationDbContext _dbContext;

    public GetAdminAddonItemsQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<AdminAddonItemDto>> Handle(GetAdminAddonItemsQuery request, CancellationToken cancellationToken)
    {
        return await _dbContext.AddonItems
            .AsNoTracking()
            .OrderBy(addon => addon.DisplayOrder)
            .ThenBy(addon => addon.Name)
            .Select(addon => new AdminAddonItemDto(
                addon.Id,
                addon.Name,
                addon.PersianName,
                addon.EnglishName,
                addon.Description,
                addon.Price,
                addon.Tags,
                addon.DisplayOrder,
                addon.IsActive))
            .ToListAsync(cancellationToken);
    }
}

