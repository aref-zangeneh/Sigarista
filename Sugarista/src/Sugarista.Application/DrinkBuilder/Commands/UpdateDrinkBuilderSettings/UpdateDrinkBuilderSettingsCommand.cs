using Microsoft.EntityFrameworkCore;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.DrinkBuilder.Commands.UpdateDrinkBuilderSettings;

public record UpdateDrinkBuilderSettingsCommand(bool IsEnabled) : IRequest<bool>;

public class UpdateDrinkBuilderSettingsCommandHandler
    : IRequestHandler<UpdateDrinkBuilderSettingsCommand, bool>
{
    private readonly IApplicationDbContext _dbContext;

    public UpdateDrinkBuilderSettingsCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> Handle(
        UpdateDrinkBuilderSettingsCommand request,
        CancellationToken cancellationToken)
    {
        var settings = await _dbContext.SiteSettings
            .FirstOrDefaultAsync(cancellationToken);

        if (settings is null)
        {
            settings = new Sugarista.Domain.Entities.SiteSettings();
            _dbContext.SiteSettings.Add(settings);
        }

        settings.CustomDrinkBuilderEnabled = request.IsEnabled;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return settings.CustomDrinkBuilderEnabled;
    }
}
