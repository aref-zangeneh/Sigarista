using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Badges.Dto;
using Sugarista.Application.Common.Interfaces;

namespace Sugarista.Application.Badges.Queries.GetAdminBadges;

public record GetAdminBadgesQuery : IRequest<IReadOnlyCollection<AdminBadgeDto>>;

public class GetAdminBadgesQueryHandler : IRequestHandler<GetAdminBadgesQuery, IReadOnlyCollection<AdminBadgeDto>>
{
    private readonly IApplicationDbContext _dbContext;

    public GetAdminBadgesQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<AdminBadgeDto>> Handle(GetAdminBadgesQuery request, CancellationToken cancellationToken)
    {
        return await _dbContext.Badges
            .AsNoTracking()
            .OrderBy(entry => entry.DisplayOrder)
            .ThenBy(entry => entry.CreatedAt)
            .Select(entry => new AdminBadgeDto(
                entry.Id,
                entry.Name,
                entry.Description,
                entry.ImageUrl,
                entry.MinPaidOrders,
                entry.MinTotalSpent,
                entry.IsEnabled,
                entry.DisplayOrder,
                entry.Awards.Count))
            .ToListAsync(cancellationToken);
    }
}
