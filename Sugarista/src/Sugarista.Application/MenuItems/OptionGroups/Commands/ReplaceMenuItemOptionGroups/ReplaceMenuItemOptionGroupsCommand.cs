using Microsoft.EntityFrameworkCore;
using Sugarista.Domain.Entities;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Application.MenuItems.OptionGroups.Commands.ReplaceMenuItemOptionGroups;

/// <summary>
/// Replaces the whole option-group configuration of one menu item. The admin
/// panel edits groups/options/ingredients as one document, so a full-replace
/// PUT keeps the client and the server state perfectly in sync.
/// </summary>
public record ReplaceMenuItemOptionGroupsCommand(
    Guid MenuItemId,
    IReadOnlyCollection<MenuItemOptionGroupInput> Groups) : IRequest;

public class ReplaceMenuItemOptionGroupsCommandHandler : IRequestHandler<ReplaceMenuItemOptionGroupsCommand>
{
    private readonly IApplicationDbContext _dbContext;

    public ReplaceMenuItemOptionGroupsCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task Handle(
        ReplaceMenuItemOptionGroupsCommand request,
        CancellationToken cancellationToken)
    {
        var menuItemExists = await _dbContext.MenuItems
            .AnyAsync(item => item.Id == request.MenuItemId, cancellationToken);

        if (!menuItemExists)
        {
            throw new DomainException("آیتم منو یافت نشد.");
        }

        await ValidateAsync(request.Groups, cancellationToken);        // Full replace: clear every existing group (and its options/ingredient
        // rows) for this item before persisting the submitted document.
        var existingGroups = await _dbContext.MenuItemOptionGroups
            .Include(group => group.Options)
            .ThenInclude(option => option.Ingredients)
            .Where(group => group.MenuItemId == request.MenuItemId)
            .ToListAsync(cancellationToken);

        foreach (var existingOption in existingGroups.SelectMany(group => group.Options))
        {
            _dbContext.MenuItemOptionIngredients.RemoveRange(existingOption.Ingredients);
        }

        _dbContext.MenuItemOptions.RemoveRange(existingGroups.SelectMany(group => group.Options));
        _dbContext.MenuItemOptionGroups.RemoveRange(existingGroups);

        foreach (var groupInput in request.Groups)
        {
            var group = new MenuItemOptionGroup(
                request.MenuItemId,
                groupInput.PersianName,
                groupInput.EnglishName,
                groupInput.IsRequired,
                groupInput.SelectionMode,
                groupInput.MinSelections,
                groupInput.MaxSelections,
                groupInput.DisplayOrder,
                groupInput.IsActive);

            foreach (var optionInput in groupInput.Options)
            {
                var option = group.AddOption(
                    optionInput.PersianName,
                    optionInput.EnglishName,
                    optionInput.PriceDelta,
                    optionInput.IsDefault,
                    optionInput.IsActive,
                    optionInput.DisplayOrder);

                foreach (var ingredientRow in optionInput.Ingredients)
                {
                    option.Ingredients.Add(
                        new MenuItemOptionIngredient(
                            option.Id,
                            ingredientRow.IngredientId,
                            ingredientRow.Quantity));
                }
            }

            _dbContext.MenuItemOptionGroups.Add(group);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task ValidateAsync(
        IReadOnlyCollection<MenuItemOptionGroupInput> groups,
        CancellationToken cancellationToken)
    {
        var knownIngredientIds = new HashSet<Guid>();

        foreach (var group in groups)
        {
            if (string.IsNullOrWhiteSpace(group.PersianName))
            {
                throw new DomainException("نام گروه آپشن الزامی است.");
            }

            if (group.Options.Count == 0)
            {
                throw new DomainException($"گروه «{group.PersianName}» باید حداقل یک گزینه داشته باشد.");
            }

            if (group.IsRequired && group.Options.All(option => !option.IsActive))
            {
                throw new DomainException(
                    $"گروه اجباری «{group.PersianName}» باید حداقل یک گزینهٔ فعال داشته باشد.");
            }

            foreach (var option in group.Options)
            {
                if (string.IsNullOrWhiteSpace(option.PersianName))
                {
                    throw new DomainException(
                        $"نام گزینه در گروه «{group.PersianName}» الزامی است.");
                }

                if (option.PriceDelta < 0)
                {
                    throw new DomainException(
                        $"مبلغ افزودهٔ گزینهٔ «{option.PersianName}» نمی‌تواند منفی باشد.");
                }

                foreach (var ingredientRow in option.Ingredients)
                {
                    if (ingredientRow.IngredientId == Guid.Empty ||
                        ingredientRow.Quantity <= 0)
                    {
                        throw new DomainException(
                            $"مقدار مصرف گزینهٔ «{option.PersianName}» نامعتبر است.");
                    }

                    knownIngredientIds.Add(ingredientRow.IngredientId);
                }
            }
        }

        // Actual existence of the referenced ingredients must be verified
        // against the database before writing anything.
        if (knownIngredientIds.Count > 0)
        {
            var existingIngredientIds = await _dbContext.Ingredients
                .AsNoTracking()
                .Where(ingredient => knownIngredientIds.Contains(ingredient.Id))
                .Select(ingredient => ingredient.Id)
                .ToListAsync(cancellationToken);
            var missing = knownIngredientIds.Except(existingIngredientIds).ToList();
            if (missing.Count > 0)
            {
                throw new DomainException("یکی از مواد اولیهٔ انتخاب‌شده یافت نشد.");
            }
        }
    }
}
