using MediatR;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.Tooltips.Dto;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Tooltips.Queries.GetActiveTooltipOverrides;

/// <summary>
/// Tooltip overrides as a key → text map for the Tip component. Any
/// authenticated admin may read it — POS cashiers don't have the tooltips
/// permission and still need the help bubbles.
/// An ACTIVE row maps to its custom text; an INACTIVE row maps to the empty
/// string, which tells the frontend to hide that tooltip completely (its
/// registry default must NOT be shown).
/// </summary>
public record GetActiveTooltipOverridesQuery : IRequest<IReadOnlyDictionary<string, string>>;

public class GetActiveTooltipOverridesQueryHandler
    : IRequestHandler<GetActiveTooltipOverridesQuery, IReadOnlyDictionary<string, string>>
{
    private readonly IApplicationDbContext _dbContext;

    public GetActiveTooltipOverridesQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyDictionary<string, string>> Handle(
        GetActiveTooltipOverridesQuery request,
        CancellationToken cancellationToken)
    {
        var overrides = await _dbContext.AdminTooltips
            .AsNoTracking()
            .Select(tooltip => new { tooltip.Key, tooltip.Text, tooltip.IsActive })
            .ToListAsync(cancellationToken);

        return overrides.ToDictionary(
            entry => entry.Key,
            entry => entry.IsActive ? entry.Text : string.Empty);
    }
}
