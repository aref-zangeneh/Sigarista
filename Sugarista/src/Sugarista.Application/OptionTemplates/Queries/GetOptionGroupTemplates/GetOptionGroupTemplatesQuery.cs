using Microsoft.EntityFrameworkCore;
using Sugarista.Application.MenuItems.OptionGroups;

namespace Sugarista.Application.OptionTemplates.Queries.GetOptionGroupTemplates;

public record GetOptionGroupTemplatesQuery : IRequest<OptionGroupTemplatesResponseDto>;

public class GetOptionGroupTemplatesQueryHandler
    : IRequestHandler<GetOptionGroupTemplatesQuery, OptionGroupTemplatesResponseDto>
{
    private readonly IApplicationDbContext _dbContext;

    public GetOptionGroupTemplatesQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<OptionGroupTemplatesResponseDto> Handle(
        GetOptionGroupTemplatesQuery request,
        CancellationToken cancellationToken)
    {
        var templates = await _dbContext.OptionGroupTemplates
            .AsNoTracking()
            .Include(t => t.Options)
            .ThenInclude(o => o.Ingredients)
            .OrderBy(t => t.CategoryLabel == null ? 1 : 0)
            .ThenBy(t => t.CategoryLabel)
            .ThenBy(t => t.DisplayOrder)
            .ThenBy(t => t.PersianName)
            .ToListAsync(cancellationToken);

        var linkedPairs = await _dbContext.MenuItemOptionGroups
            .AsNoTracking()
            .Where(g => g.SourceTemplateId != null)
            .Select(g => new { TemplateId = g.SourceTemplateId!.Value, g.MenuItemId })
            .Distinct()
            .ToListAsync(cancellationToken);

        var linkedByTemplate = linkedPairs
            .GroupBy(p => p.TemplateId)
            .ToDictionary(g => g.Key, g => (IReadOnlyCollection<Guid>)g.Select(p => p.MenuItemId).Distinct().ToList());

        var dtos = templates
            .Select(template =>
            {
                var linkedIds = linkedByTemplate.TryGetValue(template.Id, out var ids)
                    ? ids
                    : Array.Empty<Guid>();

                return new OptionGroupTemplateDto(
                    template.Id,
                    template.CategoryLabel,
                    template.PersianName,
                    template.EnglishName,
                    template.IsRequired,
                    template.SelectionMode,
                    template.MinSelections,
                    template.MaxSelections,
                    template.DisplayOrder,
                    template.IsActive,
                    linkedIds.Count,
                    linkedIds,
                    template.Options
                        .OrderBy(o => o.DisplayOrder)
                        .ThenBy(o => o.PersianName, StringComparer.Ordinal)
                        .Select(option => new AdminMenuItemOptionDto(
                            option.Id,
                            option.PersianName,
                            option.EnglishName,
                            option.PriceDelta,
                            option.IsDefault,
                            option.IsActive,
                            option.DisplayOrder,
                            option.Ingredients
                                .Select(row => new AdminMenuItemOptionIngredientDto(row.IngredientId, row.Quantity))
                                .ToList()))
                        .ToList());
            })
            .ToList();

        var categories = templates
            .Select(t => t.CategoryLabel)
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .Select(label => label!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(label => label, StringComparer.Ordinal)
            .ToList();

        return new OptionGroupTemplatesResponseDto(dtos, categories);
    }
}
