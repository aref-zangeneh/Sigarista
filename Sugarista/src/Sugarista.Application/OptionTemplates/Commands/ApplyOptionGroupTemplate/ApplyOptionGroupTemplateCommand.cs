using Microsoft.EntityFrameworkCore;
using Sugarista.Domain.Entities;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Application.OptionTemplates.Commands.ApplyOptionGroupTemplate;

/// <summary>
/// Applies (attaches) a reusable <see cref="OptionGroupTemplate"/> to one or more
/// menu items in one operation. When <see cref="UpdateExistingLinked"/> is true,
/// items that already have a group linked to this template get their group refreshed
/// with the latest template options &amp; prices; otherwise already-linked items are skipped.
/// </summary>
public record ApplyOptionGroupTemplateCommand(
    Guid TemplateId,
    IReadOnlyCollection<Guid> MenuItemIds,
    bool UpdateExistingLinked = false) : IRequest<ApplyOptionGroupTemplateResultDto>;

public class ApplyOptionGroupTemplateCommandHandler
    : IRequestHandler<ApplyOptionGroupTemplateCommand, ApplyOptionGroupTemplateResultDto>
{
    private readonly IApplicationDbContext _dbContext;

    public ApplyOptionGroupTemplateCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ApplyOptionGroupTemplateResultDto> Handle(
        ApplyOptionGroupTemplateCommand request,
        CancellationToken cancellationToken)
    {
        var targetIds = (request.MenuItemIds ?? [])
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        if (targetIds.Count == 0)
        {
            throw new DomainException("حداقل یک آیتم منو برای اعمال قالب انتخاب کنید.");
        }

        var template = await _dbContext.OptionGroupTemplates
            .AsNoTracking()
            .Include(t => t.Options)
            .ThenInclude(o => o.Ingredients)
            .FirstOrDefaultAsync(t => t.Id == request.TemplateId, cancellationToken)
            ?? throw new DomainException("قالب گروه آپشن یافت نشد.");

        if (template.Options.Count == 0)
        {
            throw new DomainException("قالب انتخاب‌شده هیچ گزینه‌ای ندارد.");
        }

        var validMenuItemIds = await _dbContext.MenuItems
            .AsNoTracking()
            .Where(item => targetIds.Contains(item.Id))
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);

        if (validMenuItemIds.Count == 0)
        {
            throw new DomainException("هیچ‌یک از آیتم‌های انتخاب‌شده یافت نشد.");
        }

        var existingGroups = await _dbContext.MenuItemOptionGroups
            .Include(g => g.Options)
            .ThenInclude(o => o.Ingredients)
            .Where(g => validMenuItemIds.Contains(g.MenuItemId))
            .ToListAsync(cancellationToken);

        var groupsByItem = existingGroups
            .GroupBy(g => g.MenuItemId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var addedCount = 0;
        var updatedCount = 0;
        var skippedCount = 0;

        foreach (var menuItemId in validMenuItemIds)
        {
            var itemGroups = groupsByItem.TryGetValue(menuItemId, out var list) ? list : [];

            var existingMatch = itemGroups.FirstOrDefault(g => g.SourceTemplateId == template.Id)
                ?? itemGroups.FirstOrDefault(g =>
                    string.Equals(g.PersianName.Trim(), template.PersianName.Trim(), StringComparison.OrdinalIgnoreCase));

            if (existingMatch is not null)
            {
                if (!request.UpdateExistingLinked)
                {
                    skippedCount++;
                    continue;
                }

                // Refresh the linked group in place, keeping its current DisplayOrder on the menu item.
                var preservedOrder = existingMatch.DisplayOrder;
                foreach (var oldOpt in existingMatch.Options)
                {
                    _dbContext.MenuItemOptionIngredients.RemoveRange(oldOpt.Ingredients);
                }
                _dbContext.MenuItemOptions.RemoveRange(existingMatch.Options);
                _dbContext.MenuItemOptionGroups.Remove(existingMatch);

                var refreshed = BuildGroupFromTemplate(menuItemId, template, preservedOrder);
                _dbContext.MenuItemOptionGroups.Add(refreshed);
                updatedCount++;
                continue;
            }

            var nextDisplayOrder = itemGroups.Count > 0
                ? itemGroups.Max(g => g.DisplayOrder) + 1
                : 0;

            var created = BuildGroupFromTemplate(menuItemId, template, nextDisplayOrder);
            _dbContext.MenuItemOptionGroups.Add(created);
            addedCount++;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new ApplyOptionGroupTemplateResultDto(addedCount, updatedCount, skippedCount);
    }

    private static MenuItemOptionGroup BuildGroupFromTemplate(
        Guid menuItemId,
        OptionGroupTemplate template,
        int displayOrder)
    {
        var group = new MenuItemOptionGroup(
            menuItemId,
            template.PersianName,
            template.EnglishName,
            template.IsRequired,
            template.SelectionMode,
            template.MinSelections,
            template.MaxSelections,
            displayOrder,
            template.IsActive,
            template.Id);

        foreach (var templateOption in template.Options.OrderBy(o => o.DisplayOrder))
        {
            var option = group.AddOption(
                templateOption.PersianName,
                templateOption.EnglishName,
                templateOption.PriceDelta,
                templateOption.IsDefault,
                templateOption.IsActive,
                templateOption.DisplayOrder);

            foreach (var ingredientRow in templateOption.Ingredients)
            {
                option.Ingredients.Add(
                    new MenuItemOptionIngredient(
                        option.Id,
                        ingredientRow.IngredientId,
                        ingredientRow.Quantity));
            }
        }

        return group;
    }
}
