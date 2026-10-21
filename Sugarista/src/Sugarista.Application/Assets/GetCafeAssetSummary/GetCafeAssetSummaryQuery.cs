using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Assets.Dto;

namespace Sugarista.Application.Assets.GetCafeAssetSummary;

public record GetCafeAssetSummaryQuery : IRequest<CafeAssetSummaryDto>;

public class GetCafeAssetSummaryQueryHandler
    : IRequestHandler<GetCafeAssetSummaryQuery, CafeAssetSummaryDto>
{
    private readonly IApplicationDbContext _dbContext;

    public GetCafeAssetSummaryQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<CafeAssetSummaryDto> Handle(
        GetCafeAssetSummaryQuery request,
        CancellationToken cancellationToken)
    {
        var assets = await _dbContext.CafeAssets
            .AsNoTracking()
            .Select(asset => new { asset.IsActive, asset.Quantity, asset.TotalValue })
            .ToListAsync(cancellationToken);

        return new CafeAssetSummaryDto(
            assets.Count(item => item.IsActive),
            assets.Count(item => !item.IsActive),
            assets.Where(item => item.IsActive).Sum(item => item.Quantity),
            assets.Where(item => item.IsActive).Sum(item => item.TotalValue),
            assets.Where(item => !item.IsActive).Sum(item => item.TotalValue));
    }
}
