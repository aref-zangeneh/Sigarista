using Microsoft.EntityFrameworkCore;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Orders.Queries.GetAdminOrderAlerts;

public record GetAdminOrderAlertsQuery(DateTimeOffset? Since = null) : IRequest<AdminOrderAlertsDto>;

public record AdminOrderAlertItemDto(
    Guid Id,
    string TrackingCode,
    string CustomerName,
    string CustomerPhone,
    string FulfillmentStatus,
    DateTimeOffset? PaidAt,
    int LineCount);

public record AdminOrderAlertsDto(
    int PendingKitchenCount,
    int ReadyCount,
    IReadOnlyCollection<AdminOrderAlertItemDto> NewPaidOrders,
    IReadOnlyCollection<AdminOrderAlertItemDto> ActiveKitchenOrders,
    IReadOnlyCollection<AdminOrderAlertItemDto> ReadyForPickupOrders,
    DateTimeOffset ServerTime);

public class GetAdminOrderAlertsQueryHandler : IRequestHandler<GetAdminOrderAlertsQuery, AdminOrderAlertsDto>
{
    private readonly IApplicationDbContext _dbContext;

    public GetAdminOrderAlertsQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AdminOrderAlertsDto> Handle(
        GetAdminOrderAlertsQuery request,
        CancellationToken cancellationToken)
    {
        var since = request.Since ?? DateTimeOffset.UtcNow.AddMinutes(-1);
        var now = DateTimeOffset.UtcNow;

        var paidOrders = await _dbContext.Orders
            .AsNoTracking()
            .Where(o => o.Status == OrderStatus.Paid)
            .Select(o => new AdminOrderAlertItemDto(
                o.Id,
                o.TrackingCode,
                o.CustomerName,
                o.CustomerPhone,
                o.FulfillmentStatus.ToString(),
                o.PaidAt,
                o.Lines.Count))
            .ToListAsync(cancellationToken);

        static bool NeedsKitchen(AdminOrderAlertItemDto order) =>
            order.FulfillmentStatus is nameof(FulfillmentStatus.Registered)
                or nameof(FulfillmentStatus.Preparing)
                or nameof(FulfillmentStatus.None)
                or "";

        var pendingKitchen = paidOrders
            .Where(NeedsKitchen)
            .ToList();

        var readyOrders = paidOrders
            .Where(o => o.FulfillmentStatus == nameof(FulfillmentStatus.Ready))
            .OrderBy(o => o.PaidAt)
            .ToList();

        var newPaidOrders = paidOrders
            .Where(o =>
                o.PaidAt.HasValue &&
                o.PaidAt.Value > since &&
                NeedsKitchen(o))
            .OrderByDescending(o => o.PaidAt)
            .ToList();

        var activeKitchenOrders = pendingKitchen
            .OrderBy(o => o.PaidAt)
            .ToList();

        return new AdminOrderAlertsDto(
            pendingKitchen.Count,
            readyOrders.Count,
            newPaidOrders,
            activeKitchenOrders,
            readyOrders,
            now);
    }
}
