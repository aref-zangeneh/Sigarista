using Microsoft.EntityFrameworkCore;

namespace Sugarista.Application.DrinkBuilder.Queries.GetDrinkBuilderSettings;

public record GetDrinkBuilderSettingsQuery : IRequest<AdminDrinkBuilderSettingsDto>;

public class GetDrinkBuilderSettingsQueryHandler
    : IRequestHandler<GetDrinkBuilderSettingsQuery, AdminDrinkBuilderSettingsDto>
{
    private readonly IApplicationDbContext _dbContext;

    public GetDrinkBuilderSettingsQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AdminDrinkBuilderSettingsDto> Handle(
        GetDrinkBuilderSettingsQuery request,
        CancellationToken cancellationToken)
    {
        var settings = await _dbContext.SiteSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        return new AdminDrinkBuilderSettingsDto(settings?.CustomDrinkBuilderEnabled ?? true);
    }
}
