using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.Discounts.Engine;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Discounts.Queries.GetDeliverySlotDiscounts;

public record DeliverySlotDiscountDto(
    DateTimeOffset Time,
    bool HasDiscount,
    decimal? DiscountPercent,
    string? DiscountName);

public record GetDeliverySlotDiscountsQuery(
    IReadOnlyCollection<DiscountLineInput> Lines,
    IReadOnlyCollection<DateTimeOffset> Slots,
    string TimeZoneId = "Asia/Tehran") : IRequest<IReadOnlyCollection<DeliverySlotDiscountDto>>;

public class GetDeliverySlotDiscountsQueryHandler : IRequestHandler<GetDeliverySlotDiscountsQuery, IReadOnlyCollection<DeliverySlotDiscountDto>>
{
    private readonly IApplicationDbContext _dbContext;

    public GetDeliverySlotDiscountsQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<DeliverySlotDiscountDto>> Handle(
        GetDeliverySlotDiscountsQuery request,
        CancellationToken cancellationToken)
    {
        var lines = request.Lines;
        if (lines.Any(line => line.CategoryId is null))
        {
            var unresolvedItemIds = lines.Where(line => line.CategoryId is null).Select(line => line.MenuItemId).Distinct().ToList();
            var categoryByItemId = await _dbContext.MenuItems
                .AsNoTracking()
                .Where(item => unresolvedItemIds.Contains(item.Id))
                .Select(item => new { item.Id, item.CategoryId })
                .ToDictionaryAsync(item => item.Id, item => item.CategoryId, cancellationToken);

            lines = lines
                .Select(line => line.CategoryId is null && categoryByItemId.TryGetValue(line.MenuItemId, out var categoryId)
                    ? line with { CategoryId = categoryId }
                    : line)
                .ToList();
        }

        var subtotal = lines.Sum(line => line.LineTotal);

        var now = DateTimeOffset.UtcNow;
        var discounts = await _dbContext.Discounts
            .AsNoTracking()
            .Include(discount => discount.MenuItems)
            .Include(discount => discount.Categories)
            .Include(discount => discount.Schedules)
            .Where(discount => discount.IsActive && discount.IsAutomatic)
            .ToListAsync(cancellationToken);

        discounts = discounts
            .Where(discount => discount.IsLive(now) || discount.EndsAt == null || discount.EndsAt > now)
            .ToList();

        var result = new List<DeliverySlotDiscountDto>();

        foreach (var slot in request.Slots.OrderBy(s => s))
        {
            DeliverySlotDiscountDto? match = null;
            foreach (var discount in discounts)
            {
                if (!discount.IsLive(slot)) continue;
                if (!discount.MatchesSchedule(slot, request.TimeZoneId)) continue;
                if (subtotal < discount.MinCartAmount) continue;

                // scope check
                if (discount.HasScopeEntries)
                {
                    var itemIds = discount.MenuItems.Select(m => m.MenuItemId).ToHashSet();
                    var categoryIds = discount.Categories.Select(c => c.CategoryId).ToHashSet();
                    var inScope = lines.Any(line =>
                        itemIds.Contains(line.MenuItemId) ||
                        (line.CategoryId is not null && categoryIds.Contains(line.CategoryId.Value)));
                    if (!inScope) continue;
                }

                decimal? percent = discount.ValueKind == DiscountValueKind.Percentage ? discount.Value : null;
                match = new DeliverySlotDiscountDto(slot, true, percent, discount.Name);
                break;
            }

            result.Add(match ?? new DeliverySlotDiscountDto(slot, false, null, null));
        }

        return result;
    }
}
