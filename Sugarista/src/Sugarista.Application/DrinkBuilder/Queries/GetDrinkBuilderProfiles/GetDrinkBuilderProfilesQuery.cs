using Microsoft.EntityFrameworkCore;

namespace Sugarista.Application.DrinkBuilder.Queries.GetDrinkBuilderProfiles;

public record GetDrinkBuilderProfilesQuery(string? Search = null)
    : IRequest<IReadOnlyCollection<AdminDrinkBuilderProfileDto>>;

public class GetDrinkBuilderProfilesQueryHandler
    : IRequestHandler<GetDrinkBuilderProfilesQuery, IReadOnlyCollection<AdminDrinkBuilderProfileDto>>
{
    private readonly IApplicationDbContext _dbContext;

    public GetDrinkBuilderProfilesQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<AdminDrinkBuilderProfileDto>> Handle(
        GetDrinkBuilderProfilesQuery request,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.DrinkBuilderProfiles
            .AsNoTracking()
            .Include(profile => profile.MenuItem)
            .Include(profile => profile.Groups)
            .ThenInclude(group => group.Options)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLowerInvariant();
            query = query.Where(profile =>
                profile.Name.ToLower().Contains(search) ||
                profile.MenuItem.PersianName.ToLower().Contains(search) ||
                profile.MenuItem.EnglishName.ToLower().Contains(search));
        }

        var profiles = await query
            .OrderBy(profile => profile.DisplayOrder)
            .ThenBy(profile => profile.Name)
            .ToListAsync(cancellationToken);

        return profiles.Select(DrinkBuilderAdminMapper.ToDto).ToArray();
    }
}
