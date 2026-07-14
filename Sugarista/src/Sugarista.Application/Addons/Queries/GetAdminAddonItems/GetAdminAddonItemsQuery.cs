using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Extensions;
using Sugarista.Application.Common.Models;

namespace Sugarista.Application.Addons.Queries.GetAdminAddonItems;

public record GetAdminAddonItemsQuery(int Page = 1, int PageSize = 25, string? Search = null)
    : IRequest<PaginatedList<AdminAddonItemDto>>;

public record AdminAddonItemDto(
    Guid Id,
    string Name,
    string PersianName,
    string EnglishName,
    string Description,
    decimal Price,
    IReadOnlyCollection<string> Tags,
    int DisplayOrder,
    bool IsActive);

public class GetAdminAddonItemsQueryHandler : IRequestHandler<GetAdminAddonItemsQuery, PaginatedList<AdminAddonItemDto>>
{
    private readonly IApplicationDbContext _dbContext;

    public GetAdminAddonItemsQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PaginatedList<AdminAddonItemDto>> Handle(GetAdminAddonItemsQuery request, CancellationToken cancellationToken)
    {
        var pagination = new PaginationQuery(request.Page, request.PageSize, request.Search);

        var query = _dbContext.AddonItems.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(pagination.Search))
        {
            var search = pagination.Search.Trim().ToLowerInvariant();
            query = query.Where(addon =>
                addon.Name.ToLower().Contains(search) ||
                addon.PersianName.ToLower().Contains(search) ||
                addon.EnglishName.ToLower().Contains(search));
        }

        return await query
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
            .ToPaginatedListAsync(pagination, cancellationToken);
    }
}
