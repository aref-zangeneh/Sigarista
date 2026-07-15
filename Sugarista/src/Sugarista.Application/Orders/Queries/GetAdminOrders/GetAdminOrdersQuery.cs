using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Extensions;
using Sugarista.Application.Common.Models;

namespace Sugarista.Application.Orders.Queries.GetAdminOrders;

public record GetAdminOrdersQuery(int Page = 1, int PageSize = 25, string? Search = null)
    : IRequest<PaginatedList<AdminOrderListItemDto>>;

public record AdminOrderListItemDto(
    Guid Id,
    string TrackingCode,
    string CustomerName,
    string CustomerPhone,
    decimal TotalAmount,
    decimal AmountPaid,
    bool IsSplitBill,
    int? GuestCount,
    string Status,
    string FulfillmentStatus,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PaidAt,
    DateTimeOffset? FulfillmentUpdatedAt,
    DateTimeOffset? ReadyAt,
    DateTimeOffset? ReadySmsSentAt,
    int LineCount);

public class GetAdminOrdersQueryHandler : IRequestHandler<GetAdminOrdersQuery, PaginatedList<AdminOrderListItemDto>>
{
    private readonly IApplicationDbContext _dbContext;

    public GetAdminOrdersQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PaginatedList<AdminOrderListItemDto>> Handle(GetAdminOrdersQuery request, CancellationToken cancellationToken)
    {
        var pagination = new PaginationQuery(request.Page, request.PageSize, request.Search);

        var query = _dbContext.Orders.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(pagination.Search))
        {
            var search = pagination.Search.Trim().ToLowerInvariant();
            query = query.Where(order =>
                order.TrackingCode.ToLower().Contains(search) ||
                order.CustomerName.ToLower().Contains(search) ||
                order.CustomerPhone.ToLower().Contains(search));
        }

        return await query
            .OrderByDescending(o => o.CreatedAt)
            .Select(order => new AdminOrderListItemDto(
                order.Id,
                order.TrackingCode,
                order.CustomerName,
                order.CustomerPhone,
                order.TotalAmount,
                order.AmountPaid,
                order.IsSplitBill,
                order.GuestCount,
                order.Status.ToString(),
                order.FulfillmentStatus.ToString(),
                order.CreatedAt,
                order.PaidAt,
                order.FulfillmentUpdatedAt,
                order.ReadyAt,
                order.ReadySmsSentAt,
                order.Lines.Count))
            .ToPaginatedListAsync(pagination, cancellationToken);
    }
}
