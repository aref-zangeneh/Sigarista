using Microsoft.EntityFrameworkCore;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.MenuItems.Commands.UpsertMenuItem;

public record UpsertMenuItemCommand(
    Guid? Id,
    Guid CategoryId,
    string Name,
    string Description,
    int Price,
    IReadOnlyCollection<string> Tags,
    string? Badge,
    bool Featured,
    string Accent,
    string ShortLabel,
    int DisplayOrder,
    bool IsActive) : IRequest<Guid>;

public class UpsertMenuItemCommandValidator : AbstractValidator<UpsertMenuItemCommand>
{
    public UpsertMenuItemCommandValidator()
    {
        RuleFor(command => command.CategoryId).NotEmpty();
        RuleFor(command => command.Name).NotEmpty().MaximumLength(200);
        RuleFor(command => command.Description).NotEmpty().MaximumLength(1000);
        RuleFor(command => command.Price).GreaterThanOrEqualTo(0);
        RuleFor(command => command.Accent).NotEmpty().MaximumLength(200);
        RuleFor(command => command.ShortLabel).NotEmpty().MaximumLength(10);
        RuleFor(command => command.DisplayOrder).GreaterThanOrEqualTo(0);
    }
}

public class UpsertMenuItemCommandHandler : IRequestHandler<UpsertMenuItemCommand, Guid>
{
    private readonly IApplicationDbContext _dbContext;

    public UpsertMenuItemCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Guid> Handle(UpsertMenuItemCommand request, CancellationToken cancellationToken)
    {
        var categoryExists = await _dbContext.Categories
            .AnyAsync(category => category.Id == request.CategoryId, cancellationToken);

        if (!categoryExists)
        {
            throw new KeyNotFoundException("Selected category was not found.");
        }

        if (request.Id is { } itemId)
        {
            var existingItem = await _dbContext.MenuItems
                .FirstOrDefaultAsync(item => item.Id == itemId, cancellationToken)
                ?? throw new KeyNotFoundException("Menu item was not found.");

            existingItem.Update(
                request.CategoryId,
                request.Name,
                request.Description,
                request.Price,
                request.Tags,
                request.Badge,
                request.Featured,
                request.Accent,
                request.ShortLabel,
                request.DisplayOrder,
                request.IsActive);
        }
        else
        {
            var menuItem = new MenuItem(
                request.CategoryId,
                request.Name,
                request.Description,
                request.Price,
                request.Tags,
                request.Badge,
                request.Featured,
                request.Accent,
                request.ShortLabel,
                request.DisplayOrder,
                request.IsActive);

            _dbContext.MenuItems.Add(menuItem);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return menuItem.Id;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return request.Id!.Value;
    }
}
