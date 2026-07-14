using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Extensions;
using Sugarista.Application.Common.Models;

namespace Sugarista.Application.Inventory.Queries.GetProductionBatches;

public record GetProductionBatchesQuery(
    int Page = 1,
    int PageSize = 25,
    string? Search = null,
    Guid? MenuItemId = null) : IRequest<PaginatedList<ProductionBatchDto>>;

public record ProductionBatchDto(
    Guid Id,
    Guid MenuItemId,
    string MenuItemPersianName,
    string MenuItemEnglishName,
    decimal QuantityProduced,
    string? Note,
    DateTimeOffset ProducedAt);

public class GetProductionBatchesQueryHandler
    : IRequestHandler<GetProductionBatchesQuery, PaginatedList<ProductionBatchDto>>
{
    private readonly IApplicationDbContext _dbContext;

    public GetProductionBatchesQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PaginatedList<ProductionBatchDto>> Handle(
        GetProductionBatchesQuery request,
        CancellationToken cancellationToken)
    {
        var pagination = new PaginationQuery(request.Page, request.PageSize, request.Search);

        var query = _dbContext.ProductionBatches
            .AsNoTracking()
            .Include(p => p.MenuItem)
            .AsQueryable();

        if (request.MenuItemId is { } menuItemId)
        {
            query = query.Where(p => p.MenuItemId == menuItemId);
        }

        if (!string.IsNullOrWhiteSpace(pagination.Search))
        {
            var search = pagination.Search.Trim().ToLowerInvariant();
            query = query.Where(p =>
                p.MenuItem.PersianName.ToLower().Contains(search) ||
                p.MenuItem.EnglishName.ToLower().Contains(search) ||
                (p.Note != null && p.Note.ToLower().Contains(search)));
        }

        return await query
            .OrderByDescending(p => p.ProducedAt)
            .Select(p => new ProductionBatchDto(
                p.Id,
                p.MenuItemId,
                p.MenuItem.PersianName,
                p.MenuItem.EnglishName,
                p.QuantityProduced,
                p.Note,
                p.ProducedAt))
            .ToPaginatedListAsync(pagination, cancellationToken);
    }
}
