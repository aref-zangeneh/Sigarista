using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Discounts.Commands.UpsertDiscount;

public record UpsertDiscountCommand(
    Guid? Id,
    string Name,
    string? Description,
    string? Code,
    DiscountType Type,
    DiscountValueKind ValueKind,
    decimal Value,
    decimal? MaxDiscountAmount,
    decimal MinCartAmount,
    DateTimeOffset? StartsAt,
    DateTimeOffset? EndsAt,
    bool IsActive,
    int Priority,
    bool IsStackable,
    int? MaxTotalUses,
    int? MaxUsesPerCustomer,
    DiscountAppliesTo AppliesTo,
    DiscountCustomerGroup CustomerGroup,
    IReadOnlyCollection<Guid> MenuItemIds,
    IReadOnlyCollection<Guid> CategoryIds,
    IReadOnlyCollection<DiscountScheduleInput> Schedules,
    IReadOnlyCollection<DiscountTierInput>? Tiers = null,
    int BuyQuantity = 1,
    int GetQuantity = 1,
    FreeItemSelection FreeItemSelection = FreeItemSelection.Cheapest,
    int? MaxApplications = null,
    bool IsFlashSale = false) : IRequest<Guid>;

public class UpsertDiscountCommandValidator : AbstractValidator<UpsertDiscountCommand>
{
    public UpsertDiscountCommandValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(200);
        RuleFor(command => command.Description).MaximumLength(1000);
        RuleFor(command => command.Code).MaximumLength(64);
        RuleFor(command => command.Type).Must(type => Enum.IsDefined(typeof(DiscountType), type)).WithMessage("The discount type is invalid.");
        RuleFor(command => command.ValueKind).Must(kind => Enum.IsDefined(typeof(DiscountValueKind), kind)).WithMessage("The discount value kind is invalid.");
        RuleFor(command => command.Value).GreaterThanOrEqualTo(0);
        RuleFor(command => command.Value)
            .LessThanOrEqualTo(100)
            .When(command => command.ValueKind == DiscountValueKind.Percentage);
        RuleFor(command => command.MaxDiscountAmount).GreaterThanOrEqualTo(0);
        RuleFor(command => command.MinCartAmount).GreaterThanOrEqualTo(0);
        RuleFor(command => command.StartsAt)
            .LessThan(command => command.EndsAt)
            .When(command => command.StartsAt.HasValue && command.EndsAt.HasValue);
        RuleFor(command => command.Priority).GreaterThanOrEqualTo(0);
        RuleFor(command => command.MaxTotalUses)
            .GreaterThan(0)
            .When(command => command.MaxTotalUses.HasValue);
        RuleFor(command => command.MaxUsesPerCustomer)
            .GreaterThan(0)
            .When(command => command.MaxUsesPerCustomer.HasValue);
        RuleForEach(command => command.Tiers).ChildRules(tier =>
        {
            tier.RuleFor(entry => entry.ThresholdAmount).GreaterThan(0);
            tier.RuleFor(entry => entry.Value).GreaterThanOrEqualTo(0);
            tier.RuleFor(entry => entry.Value)
                .LessThanOrEqualTo(100)
                .When(entry => entry.ValueKind == DiscountValueKind.Percentage);
        });
        RuleFor(command => command)
            .Must(command => command.Type != DiscountType.TieredCart || (command.Tiers is { Count: > 0 }))
            .WithMessage("A tiered cart discount needs at least one tier.")
            .OverridePropertyName("Tiers");
    }
}

public class UpsertDiscountCommandHandler : IRequestHandler<UpsertDiscountCommand, Guid>
{
    private readonly IApplicationDbContext _dbContext;

    public UpsertDiscountCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Guid> Handle(UpsertDiscountCommand request, CancellationToken cancellationToken)
    {
        Discount discount;

        if (request.Id is { } discountId)
        {
            discount = await _dbContext.Discounts
                .FirstOrDefaultAsync(discount => discount.Id == discountId, cancellationToken)
                ?? throw new KeyNotFoundException("Discount was not found.");

            discount.Update(
                request.Name,
                request.Description,
                request.Code,
                request.Type,
                request.ValueKind,
                request.Value,
                request.MaxDiscountAmount,
                request.MinCartAmount,
                request.StartsAt,
                request.EndsAt,
                request.IsActive,
                request.Priority,
                request.IsStackable,
                request.MaxTotalUses,
                request.MaxUsesPerCustomer,
                request.AppliesTo,
                request.CustomerGroup,
                request.BuyQuantity,
                request.GetQuantity,
                request.FreeItemSelection,
                request.MaxApplications,
                request.IsFlashSale);
        }
        else
        {
            discount = new Discount(
                request.Name,
                request.Description,
                request.Code,
                request.Type,
                request.ValueKind,
                request.Value,
                request.MaxDiscountAmount,
                request.MinCartAmount,
                request.StartsAt,
                request.EndsAt,
                request.IsActive,
                request.Priority,
                request.IsStackable,
                request.MaxTotalUses,
                request.MaxUsesPerCustomer,
                request.AppliesTo,
                request.CustomerGroup,
                request.BuyQuantity,
                request.GetQuantity,
                request.FreeItemSelection,
                request.MaxApplications,
                request.IsFlashSale);

            _dbContext.Discounts.Add(discount);
        }

        discount.ReplaceScope(request.MenuItemIds, request.CategoryIds, request.Schedules);
        discount.ReplaceTiers(request.Tiers ?? []);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return discount.Id;
    }
}
