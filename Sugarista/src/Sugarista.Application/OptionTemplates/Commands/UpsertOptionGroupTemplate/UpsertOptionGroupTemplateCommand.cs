using Microsoft.EntityFrameworkCore;
using Sugarista.Application.MenuItems.OptionGroups;
using Sugarista.Domain.Entities;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Application.OptionTemplates.Commands.UpsertOptionGroupTemplate;

public record UpsertOptionGroupTemplateCommand(
    Guid? Id,
    string? CategoryLabel,
    string PersianName,
    string? EnglishName,
    bool IsRequired,
    string SelectionMode,
    int MinSelections,
    int MaxSelections,
    int DisplayOrder,
    bool IsActive,
    IReadOnlyCollection<MenuItemOptionInput> Options) : IRequest<Guid>;

public class UpsertOptionGroupTemplateCommandHandler
    : IRequestHandler<UpsertOptionGroupTemplateCommand, Guid>
{
    private readonly IApplicationDbContext _dbContext;

    public UpsertOptionGroupTemplateCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Guid> Handle(
        UpsertOptionGroupTemplateCommand request,
        CancellationToken cancellationToken)
    {
        await ValidateAsync(request, cancellationToken);

        OptionGroupTemplate template;

        if (request.Id is { } templateId)
        {
            template = await _dbContext.OptionGroupTemplates
                .Include(t => t.Options)
                .ThenInclude(o => o.Ingredients)
                .FirstOrDefaultAsync(t => t.Id == templateId, cancellationToken)
                ?? throw new DomainException("قالب گروه آپشن یافت نشد.");

            template.Update(
                request.CategoryLabel,
                request.PersianName,
                request.EnglishName,
                request.IsRequired,
                request.SelectionMode,
                request.MinSelections,
                request.MaxSelections,
                request.DisplayOrder,
                request.IsActive);

            foreach (var existingOption in template.Options)
            {
                _dbContext.OptionGroupTemplateItemIngredients.RemoveRange(existingOption.Ingredients);
            }
            _dbContext.OptionGroupTemplateItems.RemoveRange(template.Options);
            template.Options.Clear();
        }
        else
        {
            template = new OptionGroupTemplate(
                request.CategoryLabel,
                request.PersianName,
                request.EnglishName,
                request.IsRequired,
                request.SelectionMode,
                request.MinSelections,
                request.MaxSelections,
                request.DisplayOrder,
                request.IsActive);

            _dbContext.OptionGroupTemplates.Add(template);
        }

        var index = 0;
        foreach (var optionInput in request.Options)
        {
            var option = template.AddOption(
                optionInput.PersianName,
                optionInput.EnglishName,
                optionInput.PriceDelta,
                optionInput.IsDefault,
                optionInput.IsActive,
                optionInput.DisplayOrder >= 0 ? optionInput.DisplayOrder : index);

            foreach (var ingredientRow in optionInput.Ingredients)
            {
                option.Ingredients.Add(
                    new OptionGroupTemplateItemIngredient(
                        option.Id,
                        ingredientRow.IngredientId,
                        ingredientRow.Quantity));
            }

            index++;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return template.Id;
    }

    private async Task ValidateAsync(
        UpsertOptionGroupTemplateCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.PersianName))
        {
            throw new DomainException("نام قالب گروه آپشن الزامی است.");
        }

        if (request.Options is null || request.Options.Count == 0)
        {
            throw new DomainException("هر قالب گروه آپشن باید حداقل یک گزینه داشته باشد.");
        }

        if (request.IsRequired && request.Options.All(o => !o.IsActive))
        {
            throw new DomainException("قالب اجباری باید حداقل یک گزینهٔ فعال داشته باشد.");
        }

        var ingredientIds = new HashSet<Guid>();

        foreach (var option in request.Options)
        {
            if (string.IsNullOrWhiteSpace(option.PersianName))
            {
                throw new DomainException("نام تمام گزینه‌های قالب الزامی است.");
            }

            if (option.PriceDelta < 0)
            {
                throw new DomainException($"مبلغ افزودهٔ گزینهٔ «{option.PersianName}» نمی‌تواند منفی باشد.");
            }

            foreach (var row in option.Ingredients)
            {
                if (row.IngredientId == Guid.Empty || row.Quantity <= 0)
                {
                    throw new DomainException($"مقدار مصرف مواد اولیهٔ گزینهٔ «{option.PersianName}» نامعتبر است.");
                }

                ingredientIds.Add(row.IngredientId);
            }
        }

        if (ingredientIds.Count > 0)
        {
            var found = await _dbContext.Ingredients
                .AsNoTracking()
                .Where(i => ingredientIds.Contains(i.Id))
                .Select(i => i.Id)
                .ToListAsync(cancellationToken);

            if (found.Count != ingredientIds.Count)
            {
                throw new DomainException("یکی از مواد اولیهٔ انتخاب‌شده یافت نشد.");
            }
        }
    }
}
