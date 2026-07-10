using Microsoft.EntityFrameworkCore;

namespace Sugarista.Application.Addons.Commands.UpsertAddonItem;

public record UpsertAddonItemCommand(
    Guid? Id,
    string PersianName,
    string EnglishName,
    string Description,
    int Price,
    IReadOnlyCollection<string> Tags,
    int DisplayOrder,
    bool IsActive) : IRequest<Guid>;

public class UpsertAddonItemCommandValidator : AbstractValidator<UpsertAddonItemCommand>
{
    public UpsertAddonItemCommandValidator()
    {
        RuleFor(command => command.PersianName).NotEmpty().MaximumLength(200);
        RuleFor(command => command.EnglishName).NotEmpty().MaximumLength(200);
        RuleFor(command => command.Description).NotEmpty().MaximumLength(500);
        RuleFor(command => command.Price).GreaterThanOrEqualTo(0);
        RuleFor(command => command.DisplayOrder).GreaterThanOrEqualTo(0);
    }
}

public class UpsertAddonItemCommandHandler : IRequestHandler<UpsertAddonItemCommand, Guid>
{
    private readonly IApplicationDbContext _dbContext;

    public UpsertAddonItemCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Guid> Handle(UpsertAddonItemCommand request, CancellationToken cancellationToken)
    {
        if (request.Id is { } addonId)
        {
            var existing = await _dbContext.AddonItems
                .FirstOrDefaultAsync(addon => addon.Id == addonId, cancellationToken)
                ?? throw new KeyNotFoundException("Addon item was not found.");

            existing.Update(
                request.PersianName,
                request.EnglishName,
                request.Description,
                request.Price,
                request.Tags,
                request.DisplayOrder,
                request.IsActive);

            await _dbContext.SaveChangesAsync(cancellationToken);
            return existing.Id;
        }

        var addonItem = new Sugarista.Domain.Entities.AddonItem(
            request.PersianName,
            request.EnglishName,
            request.Description,
            request.Price,
            request.Tags,
            request.DisplayOrder,
            request.IsActive);

        _dbContext.AddonItems.Add(addonItem);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return addonItem.Id;
    }
}

