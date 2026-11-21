using Microsoft.EntityFrameworkCore;
using Sugarista.Domain.Entities;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Application.OptionTemplates.Commands.DuplicateOptionGroupTemplate;

public record DuplicateOptionGroupTemplateCommand(Guid Id) : IRequest<Guid>;

public class DuplicateOptionGroupTemplateCommandHandler
    : IRequestHandler<DuplicateOptionGroupTemplateCommand, Guid>
{
    private readonly IApplicationDbContext _dbContext;

    public DuplicateOptionGroupTemplateCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Guid> Handle(
        DuplicateOptionGroupTemplateCommand request,
        CancellationToken cancellationToken)
    {
        var source = await _dbContext.OptionGroupTemplates
            .AsNoTracking()
            .Include(t => t.Options)
            .ThenInclude(o => o.Ingredients)
            .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken)
            ?? throw new DomainException("قالب گروه آپشن برای کپی یافت نشد.");

        var persianName = source.PersianName.Length + 6 <= 150
            ? source.PersianName + " (کپی)"
            : source.PersianName[..Math.Max(1, 144)].TrimEnd() + " (کپی)";

        string? englishName = null;
        if (!string.IsNullOrWhiteSpace(source.EnglishName))
        {
            englishName = source.EnglishName.Length + 7 <= 150
                ? source.EnglishName + " (Copy)"
                : source.EnglishName[..Math.Max(1, 143)].TrimEnd() + " (Copy)";
        }

        var clone = new OptionGroupTemplate(
            source.CategoryLabel,
            persianName,
            englishName,
            source.IsRequired,
            source.SelectionMode,
            source.MinSelections,
            source.MaxSelections,
            source.DisplayOrder + 1,
            source.IsActive);

        foreach (var sourceOption in source.Options.OrderBy(o => o.DisplayOrder))
        {
            var clonedOption = clone.AddOption(
                sourceOption.PersianName,
                sourceOption.EnglishName,
                sourceOption.PriceDelta,
                sourceOption.IsDefault,
                sourceOption.IsActive,
                sourceOption.DisplayOrder);

            foreach (var ingredientRow in sourceOption.Ingredients)
            {
                clonedOption.Ingredients.Add(
                    new OptionGroupTemplateItemIngredient(
                        clonedOption.Id,
                        ingredientRow.IngredientId,
                        ingredientRow.Quantity));
            }
        }

        _dbContext.OptionGroupTemplates.Add(clone);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return clone.Id;
    }
}
