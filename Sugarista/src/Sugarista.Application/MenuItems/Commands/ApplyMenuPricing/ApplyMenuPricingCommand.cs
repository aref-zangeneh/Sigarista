using Microsoft.EntityFrameworkCore;
using Sugarista.Application.MenuItems.Services;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.MenuItems.Commands.ApplyMenuPricing;

public record ApplyMenuPricingCommand(
    decimal? MarkupPercent = null,
    IReadOnlyList<Guid>? ItemIds = null,
    decimal? ManualPrice = null) : IRequest<IReadOnlyList<Guid>>;

public class ApplyMenuPricingCommandValidator : AbstractValidator<ApplyMenuPricingCommand>
{
    public ApplyMenuPricingCommandValidator()
    {
        RuleFor(c => c.MarkupPercent)
            .GreaterThanOrEqualTo(0)
            .When(c => c.MarkupPercent.HasValue)
            .WithMessage("Markup percent must be greater than or equal to zero.");

        RuleFor(c => c.ManualPrice)
            .GreaterThan(0)
            .When(c => c.ManualPrice.HasValue)
            .WithMessage("Manual price must be greater than zero.");
    }
}

public class ApplyMenuPricingCommandHandler : IRequestHandler<ApplyMenuPricingCommand, IReadOnlyList<Guid>>
{
    private readonly IApplicationDbContext _dbContext;

    public ApplyMenuPricingCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<Guid>> Handle(ApplyMenuPricingCommand request, CancellationToken cancellationToken)
    {
        var query = _dbContext.MenuItems
            .Include(item => item.RecipeItems)
            .ThenInclude(line => line.Ingredient)
            .AsQueryable();

        if (request.ItemIds is { Count: > 0 } ids)
        {
            query = query.Where(item => ids.Contains(item.Id));
        }

        var items = await query
            .OrderBy(item => item.DisplayOrder)
            .ToListAsync(cancellationToken);

        var updatedIds = new List<Guid>();

        var usesManualPrice = request.ManualPrice is { } manualPrice && manualPrice > 0;

        foreach (var item in items)
        {
            var cost = MenuPricingService.ComputeCostPerServing(item);
            if (!usesManualPrice && cost <= 0)
            {
                continue;
            }

            var markupPercent = request.MarkupPercent ?? item.MarkupPercent ?? 0m;
            var factors = MenuPricingService.ComputeSizeFactors(item);

            decimal? newSmall;
            decimal? newMedium;
            decimal? newLarge;

            if (usesManualPrice)
            {
                // Manual override: the typed final price applies to the item and each
                // defined size scales by its own volume-based cost share.
                var manual = request.ManualPrice!.Value;
                newSmall = ComputeManual(item.SmallPrice, manual, factors["small"]);
                newMedium = ComputeManual(item.MediumPrice, manual, factors["medium"]);
                newLarge = ComputeManual(item.LargePrice, manual, factors["large"]);
            }
            else
            {
                var markup = markupPercent / 100m;
                newSmall = ComputePrice(item.SmallPrice, cost, factors["small"], markup);
                newMedium = ComputePrice(item.MediumPrice, cost, factors["medium"], markup);
                newLarge = ComputePrice(item.LargePrice, cost, factors["large"], markup);
            }

            if (newSmall == item.SmallPrice && newMedium == item.MediumPrice && newLarge == item.LargePrice)
            {
                if (!usesManualPrice)
                {
                    item.SetMarkupPercent(markupPercent);
                }
                updatedIds.Add(item.Id);
                continue;
            }

            item.Update(
                item.CategoryId,
                item.PersianName,
                item.EnglishName,
                item.Description,
                item.ImageUrl,
                item.GalleryImageUrls,
                item.SpecialOfferPrice,
                item.TaxPercent,
                item.IsOrderable,
                item.PreparingDurationMinutes,
                item.Calories,
                item.Allergens,
                item.Volume,
                newSmall,
                item.SmallSpecialOfferPrice,
                item.SmallVolume,
                newMedium,
                item.MediumSpecialOfferPrice,
                item.MediumVolume,
                newLarge,
                item.LargeSpecialOfferPrice,
                item.LargeVolume,
                item.Tags,
                item.Badge,
                item.Featured,
                item.Accent,
                item.ShortLabel,
                item.DisplayOrder,
                item.IsActive);

            if (!usesManualPrice)
            {
                item.SetMarkupPercent(markupPercent);
            }
            updatedIds.Add(item.Id);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return updatedIds;
    }

    private static decimal? ComputeManual(decimal? existingPrice, decimal manualPrice, decimal factor)
    {
        if (!existingPrice.HasValue)
        {
            return null;
        }

        return MenuPricingService.RoundFinalPrice(manualPrice * factor);
    }

    private static decimal? ComputePrice(decimal? existingPrice, decimal cost, decimal factor, decimal markup)
    {
        if (!existingPrice.HasValue)
        {
            return null;
        }

        return MenuPricingService.RoundFinalPrice(cost * factor * (1m + markup));
    }
}
