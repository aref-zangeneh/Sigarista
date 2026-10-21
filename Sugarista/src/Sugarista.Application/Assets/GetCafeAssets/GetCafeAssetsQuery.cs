using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Assets.Dto;
using Sugarista.Application.Common.Models;

namespace Sugarista.Application.Assets.GetCafeAssets;

public record GetCafeAssetsQuery(
    string? Search = null,
    string? Category = null,
    string? Location = null,
    bool? ActiveOnly = null,
    string? SortBy = null,
    string? SortDirection = null,
    int Page = 1,
    int PageSize = 25) : IRequest<PaginatedList<CafeAssetDto>>;

public class GetCafeAssetsQueryHandler
    : IRequestHandler<GetCafeAssetsQuery, PaginatedList<CafeAssetDto>>
{
    private readonly IApplicationDbContext _dbContext;

    public GetCafeAssetsQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PaginatedList<CafeAssetDto>> Handle(
        GetCafeAssetsQuery request,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.CafeAssets
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(asset =>
                asset.PersianName.Contains(term) ||
                (asset.EnglishName != null && asset.EnglishName.Contains(term)) ||
                (asset.SerialNumber != null && asset.SerialNumber.Contains(term)) ||
                (asset.SupplierName != null && asset.SupplierName.Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(request.Category))
        {
            query = query.Where(asset => asset.Category == request.Category.Trim());
        }

        if (!string.IsNullOrWhiteSpace(request.Location))
        {
            query = query.Where(asset => asset.Location.Contains(request.Location.Trim()));
        }

        if (request.ActiveOnly.HasValue)
        {
            var isActive = request.ActiveOnly.Value;
            query = query.Where(asset => asset.IsActive == isActive);
        }

        var sortDirection = string.Equals(request.SortDirection, "asc", StringComparison.OrdinalIgnoreCase);
        var sortBy = (request.SortBy ?? "createdAt").ToLowerInvariant();
        var direction = sortDirection ? 1 : -1;

        // photos kept out of paging projection — fetched per page below
        var ids = await query
            .Select(asset => asset.Id)
            .ToListAsync(cancellationToken);

        var ordered = await _dbContext.CafeAssets
            .AsNoTracking()
            .Where(asset => ids.Contains(asset.Id))
            .ToListAsync(cancellationToken);

        // in-memory sort (asset table stays small — it is a registry ledger)
        var sorted = sortBy switch
        {
            "unitprice" => ordered.OrderBy(asset => asset.UnitPrice * direction).ToList(),
            "quantity" => ordered.OrderBy(asset => asset.Quantity * direction).ToList(),
            "totalvalue" => ordered.OrderBy(asset => asset.TotalValue * direction).ToList(),
            "persianname" => ordered.OrderBy(asset => asset.PersianName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(a => a.Id).ToList(),
            _ => sortDirection
                ? ordered.OrderBy(asset => asset.CreatedAt).ToList()
                : ordered.OrderByDescending(asset => asset.CreatedAt).ToList(),
        };

        if (sortBy == "persianname" && !sortDirection)
        {
            sorted = ordered.OrderByDescending(asset => asset.PersianName, StringComparer.OrdinalIgnoreCase).ToList();
        }

        var pageRows = sorted
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        var photoByAsset = (await _dbContext.CafeAssetPhotos
                .AsNoTracking()
                .ToListAsync(cancellationToken))
            .GroupBy(photo => photo.AssetId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var dtos = pageRows.Select(asset =>
        {
            photoByAsset.TryGetValue(asset.Id, out var photos);
            var orderedPhotos = (photos ?? [])
                .OrderByDescending(photo => photo.IsPrimary)
                .Select(CafeAssetMapper.ToDto)
                .ToList();
            return new CafeAssetDto(
                asset.Id,
                asset.PersianName,
                asset.EnglishName,
                asset.Category,
                asset.SerialNumber,
                asset.Location,
                asset.PurchaseDate,
                asset.UnitPrice,
                asset.Quantity,
                asset.TotalValue,
                asset.SupplierName,
                asset.Note,
                asset.IsActive,
                asset.DisposalNote,
                asset.DisposedAt,
                orderedPhotos);
        }).ToList();

        return new PaginatedList<CafeAssetDto>(
            dtos,
            sorted.Count,
            request.Page,
            request.PageSize);
    }
}
