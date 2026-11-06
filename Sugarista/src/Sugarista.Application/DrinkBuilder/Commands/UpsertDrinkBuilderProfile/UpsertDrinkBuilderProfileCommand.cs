using Microsoft.EntityFrameworkCore;
using Sugarista.Domain.Entities;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Application.DrinkBuilder.Commands.UpsertDrinkBuilderProfile;

public record DrinkBuilderOptionInput(
    Guid? Id,
    string Name,
    string? EnglishName,
    string Unit,
    decimal PricePerUnit,
    decimal MinAmount,
    decimal MaxAmount,
    decimal Step,
    decimal DefaultAmount,
    bool IsDefault,
    bool IsActive,
    int DisplayOrder);

public record DrinkBuilderGroupInput(
    Guid? Id,
    string Name,
    string InputType,
    string SelectionMode,
    int MinSelections,
    int MaxSelections,
    bool IsRequired,
    int DisplayOrder,
    IReadOnlyCollection<DrinkBuilderOptionInput> Options);

public record UpsertDrinkBuilderProfileCommand(
    Guid? Id,
    Guid MenuItemId,
    string Name,
    bool IsEnabled,
    int DisplayOrder,
    IReadOnlyCollection<DrinkBuilderGroupInput> Groups) : IRequest<Guid>;

public class UpsertDrinkBuilderProfileCommandValidator : AbstractValidator<UpsertDrinkBuilderProfileCommand>
{
    public UpsertDrinkBuilderProfileCommandValidator()
    {
        RuleFor(command => command.MenuItemId).NotEmpty();
        RuleFor(command => command.Name).NotEmpty().MaximumLength(150);
        RuleFor(command => command.DisplayOrder).GreaterThanOrEqualTo(0);
        RuleFor(command => command.Groups).NotEmpty()
            .WithMessage("حداقل یک گروه برای نوشیدنی سفارشی لازم است.");

        RuleForEach(command => command.Groups).ChildRules(group =>
        {
            group.RuleFor(entry => entry.Name).NotEmpty().MaximumLength(150);
            group.RuleFor(entry => entry.InputType)
                .Must(value => Enum.TryParse<DrinkBuilderInputType>(value, true, out _))
                .WithMessage("نوع ورودی گروه باید Slider یا Choice باشد.");
            group.RuleFor(entry => entry.SelectionMode)
                .Must(value => Enum.TryParse<DrinkBuilderSelectionMode>(value, true, out _))
                .WithMessage("حالت انتخاب گروه باید Single یا Multiple باشد.");
            group.RuleFor(entry => entry.MinSelections).GreaterThanOrEqualTo(0);
            group.RuleFor(entry => entry.MaxSelections).GreaterThanOrEqualTo(1);
            group.RuleFor(entry => entry.DisplayOrder).GreaterThanOrEqualTo(0);
            group.RuleFor(entry => entry.Options).NotEmpty()
                .WithMessage("هر گروه باید حداقل یک گزینه داشته باشد.");

            group.RuleForEach(entry => entry.Options).ChildRules(option =>
            {
                option.RuleFor(entry => entry.Name).NotEmpty().MaximumLength(150);
                option.RuleFor(entry => entry.Unit)
                    .Must(value => Enum.TryParse<DrinkBuilderUnit>(value, true, out _))
                    .WithMessage("واحد اندازه‌گیری گزینه معتبر نیست.");
                option.RuleFor(entry => entry.PricePerUnit).GreaterThanOrEqualTo(0);
                option.RuleFor(entry => entry.DisplayOrder).GreaterThanOrEqualTo(0);
                option.RuleFor(entry => entry.MinAmount).GreaterThanOrEqualTo(0);
                option.RuleFor(entry => entry.MaxAmount).GreaterThanOrEqualTo(entry => entry.MinAmount);
                option.RuleFor(entry => entry.DefaultAmount).GreaterThanOrEqualTo(0);
            });

            group.RuleFor(entry => entry).Custom((entry, context) =>
            {
                if (!Enum.TryParse<DrinkBuilderInputType>(entry.InputType, true, out var inputType) ||
                    inputType != DrinkBuilderInputType.Slider)
                {
                    return;
                }

                foreach (var option in entry.Options)
                {
                    if (option.Step <= 0)
                    {
                        context.AddFailure($"گام تغییر مقدار «{option.Name}» باید بزرگ‌تر از صفر باشد.");
                        continue;
                    }

                    if (option.DefaultAmount != 0m &&
                        (option.DefaultAmount < option.MinAmount || option.DefaultAmount > option.MaxAmount))
                    {
                        context.AddFailure($"مقدار پیش‌فرض «{option.Name}» باید بین حداقل و حداکثر مقدار باشد.");
                        continue;
                    }

                    if (option.DefaultAmount != 0m &&
                        !DrinkBuilderOption.IsAligned(option.DefaultAmount, option.MinAmount, option.Step))
                    {
                        context.AddFailure($"مقدار پیش‌فرض «{option.Name}» باید با گام تغییر مقدار هم‌تراز باشد.");
                    }
                }
            });
        });
    }
}

public class UpsertDrinkBuilderProfileCommandHandler : IRequestHandler<UpsertDrinkBuilderProfileCommand, Guid>
{
    private readonly IApplicationDbContext _dbContext;

    public UpsertDrinkBuilderProfileCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Guid> Handle(UpsertDrinkBuilderProfileCommand request, CancellationToken cancellationToken)
    {
        var menuItemExists = await _dbContext.MenuItems
            .AnyAsync(item => item.Id == request.MenuItemId, cancellationToken);

        if (!menuItemExists)
        {
            throw new KeyNotFoundException("Menu item was not found.");
        }

        DrinkBuilderProfile profile;

        if (request.Id is { } profileId)
        {
            profile = await _dbContext.DrinkBuilderProfiles
                .Include(entry => entry.Groups)
                .ThenInclude(group => group.Options)
                .FirstOrDefaultAsync(entry => entry.Id == profileId, cancellationToken)
                ?? throw new KeyNotFoundException("Drink builder profile was not found.");

            var duplicate = await _dbContext.DrinkBuilderProfiles
                .AnyAsync(entry => entry.MenuItemId == request.MenuItemId && entry.Id != profile.Id, cancellationToken);

            if (duplicate)
            {
                throw new DomainException("برای این آیتم منو قبلاً یک سازندهٔ نوشیدنی تعریف شده است.");
            }

            profile.ChangeMenuItem(request.MenuItemId);
            profile.Update(request.Name, request.IsEnabled, request.DisplayOrder);
        }
        else
        {
            var duplicate = await _dbContext.DrinkBuilderProfiles
                .AnyAsync(entry => entry.MenuItemId == request.MenuItemId, cancellationToken);

            if (duplicate)
            {
                throw new DomainException("برای این آیتم منو قبلاً یک سازندهٔ نوشیدنی تعریف شده است.");
            }

            profile = new DrinkBuilderProfile(
                request.MenuItemId,
                request.Name,
                request.IsEnabled,
                request.DisplayOrder);

            _dbContext.DrinkBuilderProfiles.Add(profile);
        }

        SyncGroups(profile, request.Groups);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return profile.Id;
    }

    private void SyncGroups(DrinkBuilderProfile profile, IReadOnlyCollection<DrinkBuilderGroupInput> groups)
    {
        var existingGroups = profile.Groups.ToList();
        var requestedGroupIds = groups
            .Where(group => group.Id.HasValue)
            .Select(group => group.Id!.Value)
            .ToHashSet();

        foreach (var group in existingGroups.Where(group => !requestedGroupIds.Contains(group.Id)).ToList())
        {
            _dbContext.DrinkBuilderGroups.Remove(group);
            profile.Groups.Remove(group);
        }

        foreach (var input in groups)
        {
            var inputType = Enum.Parse<DrinkBuilderInputType>(input.InputType, true);
            var isChoice = inputType == DrinkBuilderInputType.Choice;

            // Slider groups are always multi-select: each option is an independent
            // amount. Choice groups keep the admin's cardinality settings.
            var selectionMode = isChoice
                ? Enum.Parse<DrinkBuilderSelectionMode>(input.SelectionMode, true)
                : DrinkBuilderSelectionMode.Multiple;
            var minSelections = isChoice ? input.MinSelections : 0;
            var maxSelections = isChoice
                ? input.MaxSelections
                : Math.Max(1, input.Options.Count);
            var isRequired = isChoice && input.IsRequired;

            DrinkBuilderGroup group;

            if (input.Id is { } groupId)
            {
                group = existingGroups.FirstOrDefault(entry => entry.Id == groupId)
                    ?? throw new KeyNotFoundException("Drink builder group was not found.");

                group.Update(
                    input.Name,
                    inputType,
                    selectionMode,
                    minSelections,
                    maxSelections,
                    isRequired,
                    input.DisplayOrder);
            }
            else
            {
                group = new DrinkBuilderGroup(
                    profile.Id,
                    input.Name,
                    inputType,
                    selectionMode,
                    minSelections,
                    maxSelections,
                    isRequired,
                    input.DisplayOrder);

                profile.Groups.Add(group);
                _dbContext.DrinkBuilderGroups.Add(group);
            }

            SyncOptions(group, inputType, input.Options);

            if (isChoice &&
                selectionMode == DrinkBuilderSelectionMode.Single &&
                group.Options.Count(option => option.IsActive && option.IsDefault) > 1)
            {
                throw new DomainException($"در گروه «{group.Name}» فقط یک گزینه می‌تواند پیش‌فرض باشد.");
            }
        }
    }

    private void SyncOptions(
        DrinkBuilderGroup group,
        DrinkBuilderInputType inputType,
        IReadOnlyCollection<DrinkBuilderOptionInput> options)
    {
        var existingOptions = group.Options.ToList();
        var requestedOptionIds = options
            .Where(option => option.Id.HasValue)
            .Select(option => option.Id!.Value)
            .ToHashSet();

        foreach (var option in existingOptions.Where(entry => !requestedOptionIds.Contains(entry.Id)).ToList())
        {
            _dbContext.DrinkBuilderOptions.Remove(option);
            group.Options.Remove(option);
        }

        foreach (var input in options)
        {
            var unit = Enum.Parse<DrinkBuilderUnit>(input.Unit, true);
            var isChoice = inputType == DrinkBuilderInputType.Choice;

            // Choice options are binary: selected (1) or not (0), flat price.
            var minAmount = isChoice ? 0m : input.MinAmount;
            var maxAmount = isChoice ? 1m : input.MaxAmount;
            var step = isChoice ? 1m : input.Step;
            var defaultAmount = isChoice ? (input.IsDefault ? 1m : 0m) : input.DefaultAmount;
            var isDefault = isChoice ? input.IsDefault : input.DefaultAmount > 0m;

            if (input.Id is { } optionId)
            {
                var option = existingOptions.FirstOrDefault(entry => entry.Id == optionId)
                    ?? throw new KeyNotFoundException("Drink builder option was not found.");

                option.Update(
                    input.Name,
                    input.EnglishName,
                    unit,
                    input.PricePerUnit,
                    minAmount,
                    maxAmount,
                    step,
                    defaultAmount,
                    isDefault,
                    input.IsActive,
                    input.DisplayOrder);
            }
            else
            {
                var option = new DrinkBuilderOption(
                    group.Id,
                    input.Name,
                    input.EnglishName,
                    unit,
                    input.PricePerUnit,
                    minAmount,
                    maxAmount,
                    step,
                    defaultAmount,
                    isDefault,
                    input.IsActive,
                    input.DisplayOrder);

                group.Options.Add(option);
                _dbContext.DrinkBuilderOptions.Add(option);
            }
        }
    }
}
