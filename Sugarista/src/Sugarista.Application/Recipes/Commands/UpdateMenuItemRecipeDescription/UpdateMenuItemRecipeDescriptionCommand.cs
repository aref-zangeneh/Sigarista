using Microsoft.EntityFrameworkCore;

namespace Sugarista.Application.Recipes.Commands.UpdateMenuItemRecipeDescription;

public record UpdateMenuItemRecipeDescriptionCommand(Guid MenuItemId, string? Description) : IRequest;

public class UpdateMenuItemRecipeDescriptionCommandValidator : AbstractValidator<UpdateMenuItemRecipeDescriptionCommand>
{
    public UpdateMenuItemRecipeDescriptionCommandValidator()
    {
        RuleFor(c => c.MenuItemId).NotEmpty();
        RuleFor(c => c.Description).MaximumLength(4000);
    }
}

public class UpdateMenuItemRecipeDescriptionCommandHandler : IRequestHandler<UpdateMenuItemRecipeDescriptionCommand>
{
    private readonly IApplicationDbContext _dbContext;

    public UpdateMenuItemRecipeDescriptionCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task Handle(UpdateMenuItemRecipeDescriptionCommand request, CancellationToken cancellationToken)
    {
        var menuItem = await _dbContext.MenuItems
            .FirstOrDefaultAsync(m => m.Id == request.MenuItemId, cancellationToken)
            ?? throw new KeyNotFoundException("Menu item was not found.");

        menuItem.UpdateRecipeDescription(request.Description);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
