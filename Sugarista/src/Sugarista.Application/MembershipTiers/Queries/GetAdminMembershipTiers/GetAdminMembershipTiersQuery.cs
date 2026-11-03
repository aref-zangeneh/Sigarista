using Microsoft.EntityFrameworkCore;
using MediatR;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.MembershipTiers.Commands.UpsertMembershipTier;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.MembershipTiers.Queries.GetAdminMembershipTiers;

public record GetAdminMembershipTiersQuery : IRequest<IReadOnlyCollection<AdminMembershipTierDto>>;

public class GetAdminMembershipTiersQueryHandler
    : IRequestHandler<GetAdminMembershipTiersQuery, IReadOnlyCollection<AdminMembershipTierDto>>
{
    private readonly IApplicationDbContext _dbContext;

    public GetAdminMembershipTiersQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<AdminMembershipTierDto>> Handle(
        GetAdminMembershipTiersQuery request,
        CancellationToken cancellationToken)
    {
        var tiers = await _dbContext.MembershipTiers
            .AsNoTracking()
            .OrderBy(entry => entry.DisplayOrder)
            .ThenBy(entry => entry.MinTotalPaidSpent)
            .ToListAsync(cancellationToken);

        return tiers
            .Select(AdminMembershipTierMapper.ToDto)
            .ToList();
    }
}
