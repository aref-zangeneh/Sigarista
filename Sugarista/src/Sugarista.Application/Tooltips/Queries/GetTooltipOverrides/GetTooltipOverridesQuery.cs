using MediatR;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.Tooltips.Dto;

namespace Sugarista.Application.Tooltips.Queries.GetTooltipOverrides;

public record GetTooltipOverridesQuery : IRequest<IReadOnlyCollection<AdminTooltipOverrideDto>>;

public class GetTooltipOverridesQueryHandler
    : IRequestHandler<GetTooltipOverridesQuery, IReadOnlyCollection<AdminTooltipOverrideDto>>
{
    private readonly IApplicationDbContext _dbContext;

    public GetTooltipOverridesQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<AdminTooltipOverrideDto>> Handle(
        GetTooltipOverridesQuery request,
        CancellationToken cancellationToken)
    {
        return await _dbContext.AdminTooltips
            .AsNoTracking()
            .OrderBy(tooltip => tooltip.Key)
            .Select(tooltip => new AdminTooltipOverrideDto(
                tooltip.Key,
                tooltip.Text,
                tooltip.IsActive))
            .ToListAsync(cancellationToken);
    }
}
