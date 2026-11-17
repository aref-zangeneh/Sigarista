using MediatR;
using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.Warehouses.Dto;

namespace Sugarista.Application.Warehouses.Queries.GetWarehouses;

public record GetWarehousesQuery : IRequest<IReadOnlyCollection<WarehouseDto>>;

public class GetWarehousesQueryHandler
    : IRequestHandler<GetWarehousesQuery, IReadOnlyCollection<WarehouseDto>>
{
    private readonly IApplicationDbContext _dbContext;

    public GetWarehousesQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<WarehouseDto>> Handle(
        GetWarehousesQuery request,
        CancellationToken cancellationToken)
    {
        var warehouses = await _dbContext.Warehouses
            .AsNoTracking()
            .OrderBy(w => w.DisplayOrder)
            .ThenBy(w => w.Name)
            .ToListAsync(cancellationToken);

        var parentNames = warehouses.ToDictionary(w => w.Id, w => w.Name);
        var childCounts = warehouses
            .Where(w => w.ParentId is not null)
            .GroupBy(w => w.ParentId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        return warehouses
            .Select(w => w.ToDto(
                w.ParentId is { } parentId && parentNames.TryGetValue(parentId, out var parentName)
                    ? parentName
                    : null,
                childCounts.TryGetValue(w.Id, out var count) ? count : 0))
            .ToList();
    }
}
