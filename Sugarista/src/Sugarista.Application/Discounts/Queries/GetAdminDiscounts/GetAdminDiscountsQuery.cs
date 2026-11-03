using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Extensions;
using Sugarista.Application.Common.Models;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Discounts.Queries.GetAdminDiscounts;

public record GetAdminDiscountsQuery(int Page = 1, int PageSize = 25, string? Search = null, string? Status = null)
    : IRequest<PaginatedList<AdminDiscountDto>>;

public record AdminDiscountScheduleDto(
    DayOfWeek? DayOfWeek,
    TimeOnly? StartTime,
    TimeOnly? EndTime);

public record AdminDiscountDto(
    Guid Id,
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
    IReadOnlyCollection<AdminDiscountTierDto> Tiers,
    int BuyQuantity,
    int GetQuantity,
    FreeItemSelection FreeItemSelection,
    int? MaxApplications,
    bool IsFlashSale,
    IReadOnlyCollection<AdminDiscountScheduleDto> Schedules);

public record AdminDiscountTierDto(
    Guid Id,
    decimal ThresholdAmount,
    DiscountValueKind ValueKind,
    decimal Value);

public class GetAdminDiscountsQueryHandler : IRequestHandler<GetAdminDiscountsQuery, PaginatedList<AdminDiscountDto>>
{
    private readonly IApplicationDbContext _dbContext;

    public GetAdminDiscountsQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PaginatedList<AdminDiscountDto>> Handle(GetAdminDiscountsQuery request, CancellationToken cancellationToken)
    {
        var pagination = new PaginationQuery(request.Page, request.PageSize, request.Search);

        IQueryable<Discount> query = _dbContext.Discounts
            .AsNoTracking()
            .Include(discount => discount.Tiers)
            .Include(discount => discount.Schedules);

        if (!string.IsNullOrWhiteSpace(pagination.Search))
        {
            var search = pagination.Search.Trim().ToLowerInvariant();
            query = query.Where(discount =>
                discount.Name.ToLower().Contains(search) ||
                discount.Code!.ToLower().Contains(search));
        }

        var now = DateTimeOffset.UtcNow;
        var status = request.Status?.Trim().ToLowerInvariant();

        IQueryable<Discount> filtered = query;

        switch (status)
        {
            case "active":
                filtered = filtered.Where(discount =>
                    discount.IsActive &&
                    (discount.StartsAt == null || discount.StartsAt <= now) &&
                    (discount.EndsAt == null || discount.EndsAt >= now));
                break;
            case "scheduled":
                filtered = filtered.Where(discount => discount.IsActive && discount.StartsAt != null && discount.StartsAt > now);
                break;
            case "expired":
                filtered = filtered.Where(discount => discount.IsActive && discount.EndsAt != null && discount.EndsAt < now);
                break;
            case "inactive":
                filtered = filtered.Where(discount => !discount.IsActive);
                break;
        }

        return await filtered
            .OrderByDescending(discount => discount.Priority)
            .ThenByDescending(discount => discount.Value)
            .ThenBy(discount => discount.Name)
            .Select(discount => new AdminDiscountDto(
                discount.Id,
                discount.Name,
                discount.Description,
                discount.Code,
                discount.Type,
                discount.ValueKind,
                discount.Value,
                discount.MaxDiscountAmount,
                discount.MinCartAmount,
                discount.StartsAt,
                discount.EndsAt,
                discount.IsActive,
                discount.Priority,
                discount.IsStackable,
                discount.MaxTotalUses,
                discount.MaxUsesPerCustomer,
                discount.AppliesTo,
                discount.CustomerGroup,
                discount.Tiers
                    .OrderBy(tier => tier.ThresholdAmount)
                    .Select(tier => new AdminDiscountTierDto(tier.Id, tier.ThresholdAmount, tier.ValueKind, tier.Value))
                    .ToList(),
                discount.BuyQuantity,
                discount.GetQuantity,
                discount.FreeItemSelection,
                discount.MaxApplications,
                discount.IsFlashSale,
                discount.Schedules
                    .Select(schedule => new AdminDiscountScheduleDto(schedule.DayOfWeek, schedule.StartTime, schedule.EndTime))
                    .ToList()))
            .ToPaginatedListAsync(pagination, cancellationToken);
    }
}
