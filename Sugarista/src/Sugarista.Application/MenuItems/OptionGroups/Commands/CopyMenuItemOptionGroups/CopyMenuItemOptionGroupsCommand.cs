using Microsoft.EntityFrameworkCore;
using Sugarista.Domain.Entities;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Application.MenuItems.OptionGroups.Commands.CopyMenuItemOptionGroups;

public record CopyMenuItemOptionGroupsCommand(
    Guid SourceMenuItemId,
    IReadOnlyCollection<Guid> TargetMenuItemIds,
    IReadOnlyCollection<Guid>? GroupIds = null,
    bool ReplaceExisting = false) : IRequest<CopyOptionGroupsResultDto>;

public record CopyOptionGroupsResultDto(
    int UpdatedMenuItemCount,
    int CopiedGroupCount,
    int SkippedGroupCount);

public class CopyMenuItemOptionGroupsCommandHandler
    : IRequestHandler<CopyMenuItemOptionGroupsCommand, CopyOptionGroupsResultDto>
{
    private readonly IApplicationDbContext _dbContext;

    public CopyMenuItemOptionGroupsCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<CopyOptionGroupsResultDto> Handle(
        CopyMenuItemOptionGroupsCommand request,
        CancellationToken cancellationToken)
    {
        var targetIds = (request.TargetMenuItemIds ?? [])
            .Where(id => id != Guid.Empty && id != request.SourceMenuItemId)
            .Distinct()
            .ToList();

        if (targetIds.Count == 0)
        {
            throw new DomainException("حداقل یک آیتم مقصد (متفاوت از آیتم مبدا) انتخاب کنید.");
        }

        var sourceExists = await _dbContext.MenuItems
            .AsNoTracking()
            .AnyAsync(item => item.Id == request.SourceMenuItemId, cancellationToken);

        if (!sourceExists)
        {
            throw new DomainException("آیتم مبدا یافت نشد.");
        }

        var sourceQuery = _dbContext.MenuItemOptionGroups
            .AsNoTracking()
            .Include(group => group.Options)
            .ThenInclude(option => option.Ingredients)
            .Where(group => group.MenuItemId == request.SourceMenuItemId);

        if (request.GroupIds is { Count: > 0 })
        {
            var filterIds = request.GroupIds.Distinct().ToList();
            sourceQuery = sourceQuery.Where(group => filterIds.Contains(group.Id));
        }

        var sourceGroups = await sourceQuery
            .OrderBy(group => group.DisplayOrder)
            .ToListAsync(cancellationToken);

        if (sourceGroups.Count == 0)
        {
            throw new DomainException("گروه آپشنی در آیتم مبدا برای کپی یافت نشد.");
        }

        var validTargetIds = await _dbContext.MenuItems
            .AsNoTracking()
            .Where(item => targetIds.Contains(item.Id))
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);

        if (validTargetIds.Count == 0)
        {
            throw new DomainException("هیچ‌یک از آیتم‌های مقصد انتخاب‌شده یافت نشد.");
        }

        var existingTargetGroups = await _dbContext.MenuItemOptionGroups
            .Include(group => group.Options)
            .ThenInclude(option => option.Ingredients)
            .Where(group => validTargetIds.Contains(group.MenuItemId))
            .ToListAsync(cancellationToken);

        var groupsByTarget = existingTargetGroups
            .GroupBy(group => group.MenuItemId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var updatedItems = 0;
        var copiedGroups = 0;
        var skippedGroups = 0;

        foreach (var targetId in validTargetIds)
        {
            var currentGroups = groupsByTarget.TryGetValue(targetId, out var list) ? list : [];

            if (request.ReplaceExisting && currentGroups.Count > 0)
            {
                foreach (var existingOption in currentGroups.SelectMany(g => g.Options))
                {
                    _dbContext.MenuItemOptionIngredients.RemoveRange(existingOption.Ingredients);
                }
                _dbContext.MenuItemOptions.RemoveRange(currentGroups.SelectMany(g => g.Options));
                _dbContext.MenuItemOptionGroups.RemoveRange(currentGroups);
                currentGroups = [];
            }

            var existingNames = currentGroups
                .Select(g => g.PersianName.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var nextDisplayOrder = currentGroups.Count > 0
                ? currentGroups.Max(g => g.DisplayOrder) + 1
                : 0;

            var itemAddedAny = false;

            foreach (var sourceGroup in sourceGroups)
            {
                if (!request.ReplaceExisting && existingNames.Contains(sourceGroup.PersianName.Trim()))
                {
                    skippedGroups++;
                    continue;
                }

                var clonedGroup = new MenuItemOptionGroup(
                    targetId,
                    sourceGroup.PersianName,
                    sourceGroup.EnglishName,
                    sourceGroup.IsRequired,
                    sourceGroup.SelectionMode,
                    sourceGroup.MinSelections,
                    sourceGroup.MaxSelections,
                    nextDisplayOrder++,
                    sourceGroup.IsActive,
                    sourceGroup.SourceTemplateId);

                foreach (var sourceOption in sourceGroup.Options.OrderBy(o => o.DisplayOrder))
                {
                    var clonedOption = clonedGroup.AddOption(
                        sourceOption.PersianName,
                        sourceOption.EnglishName,
                        sourceOption.PriceDelta,
                        sourceOption.IsDefault,
                        sourceOption.IsActive,
                        sourceOption.DisplayOrder);

                    foreach (var ingredientRow in sourceOption.Ingredients)
                    {
                        clonedOption.Ingredients.Add(
                            new MenuItemOptionIngredient(
                                clonedOption.Id,
                                ingredientRow.IngredientId,
                                ingredientRow.Quantity));
                    }
                }

                _dbContext.MenuItemOptionGroups.Add(clonedGroup);
                existingNames.Add(sourceGroup.PersianName.Trim());
                copiedGroups++;
                itemAddedAny = true;
            }

            if (itemAddedAny || request.ReplaceExisting)
            {
                updatedItems++;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new CopyOptionGroupsResultDto(updatedItems, copiedGroups, skippedGroups);
    }
}
