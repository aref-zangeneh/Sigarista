using Microsoft.EntityFrameworkCore;
using Sugarista.Application.MenuItems.Services;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.MenuItems.Commands.ApplyMenuPricing;

public record ApplyMenuPricingCommand(
    decimal? MarkupPercent = null,
    IReadOnlyList<Guid>? ItemIds = null) : IRequest<IReadOnlyList<Guid>>;

public class ApplyMenuPricingCommandValidator : AbstractValidator<ApplyMenuPricingCommand>
{
    public ApplyMenuPricingCommandValidator()
    {
        RuleFor(c => c.MarkupPercent)
            .GreaterThanOrEqualTo(0)
            .When(c => c.MarkupPercent.HasValue)
            .WithMessage("Markup percent must be greater than or equal to zero.");
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

        foreach (var item in items)
        {
            var cost = MenuPricingService.ComputeCostPerServing(item);
            if (cost <= 0)
            {
                continue;
            }

            var markupPercent = request.MarkupPercent ?? item.MarkupPercent ?? 0m;
            var markup = markupPercent / 100m;
            var factors = MenuPricingService.ComputeSizeFactors(item);

            var newSmall = ComputePrice(item.SmallPrice, cost, factors["small"], markup);
            var newMedium = ComputePrice(item.MediumPrice, cost, factors["medium"], markup);
            var newLarge = ComputePrice(item.LargePrice, cost, factors["large"], markup);

            if (newSmall == item.SmallPrice && newMedium == item.MediumPrice && newLarge == item.LargePrice)
            {
                item.SetMarkupPercent(markupPercent);
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

            item.SetMarkupPercent(markupPercent);
            updatedIds.Add(item.Id);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return updatedIds;
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
