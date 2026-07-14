using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Extensions;
using Sugarista.Application.Common.Models;

namespace Sugarista.Application.Modifiers.Queries.GetAdminMenuModifiers;

public record GetAdminMenuModifiersQuery(int Page = 1, int PageSize = 25, string? Search = null)
    : IRequest<PaginatedList<AdminMenuModifierDto>>;

public record AdminMenuModifierDto(
    Guid Id,
    string Name,
    string PersianName,
    string EnglishName,
    int DisplayOrder,
    bool IsActive);

public class GetAdminMenuModifiersQueryHandler : IRequestHandler<GetAdminMenuModifiersQuery, PaginatedList<AdminMenuModifierDto>>
{
    private readonly IApplicationDbContext _dbContext;

    public GetAdminMenuModifiersQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PaginatedList<AdminMenuModifierDto>> Handle(GetAdminMenuModifiersQuery request, CancellationToken cancellationToken)
    {
        var pagination = new PaginationQuery(request.Page, request.PageSize, request.Search);

        var query = _dbContext.MenuModifiers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(pagination.Search))
        {
            var search = pagination.Search.Trim().ToLowerInvariant();
            query = query.Where(modifier =>
                modifier.Name.ToLower().Contains(search) ||
                modifier.PersianName.ToLower().Contains(search) ||
                modifier.EnglishName.ToLower().Contains(search));
        }

        return await query
            .OrderBy(modifier => modifier.DisplayOrder)
            .ThenBy(modifier => modifier.Name)
            .Select(modifier => new AdminMenuModifierDto(
                modifier.Id,
                modifier.Name,
                modifier.PersianName,
                modifier.EnglishName,
                modifier.DisplayOrder,
                modifier.IsActive))
            .ToPaginatedListAsync(pagination, cancellationToken);
    }
}
