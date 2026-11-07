using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Pickup;

namespace Sugarista.Application.Orders.Queries.GetOrderByTrackingCode;

public record GetOrderByTrackingCodeQuery(string TrackingCode) : IRequest<OrderStatusDto?>;

public record OrderStatusDto(
    string TrackingCode,
    string Status,
    string FulfillmentStatus,
    string FulfillmentStatusLabel,
    decimal TotalAmount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PaidAt,
    DateTimeOffset? ReadyAt,
    IReadOnlyCollection<OrderStatusLineDto> Lines,
    Guid Id = default,
    DateTimeOffset? DeliveryAt = null,
    DateTimeOffset? EstimatedReadyAt = null,
    bool PickupScheduled = false,
    bool CanCancel = false,
    bool CanReschedule = false,
    string? ChangeBlockedReason = null,
    string? RefundPolicyNote = null,
    int CancelCutoffMinutes = 0);

public record OrderStatusLineDto(
    string PersianName,
    string EnglishName,
    string? SizeLabel,
    int Quantity,
    decimal LineTotal,
    IReadOnlyCollection<string> ModifierLabels);

public class GetOrderByTrackingCodeQueryHandler : IRequestHandler<GetOrderByTrackingCodeQuery, OrderStatusDto?>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IDateTime _clock;

    public GetOrderByTrackingCodeQueryHandler(IApplicationDbContext dbContext, IDateTime clock)
    {
        _dbContext = dbContext;
        _clock = clock;
    }

    public async Task<OrderStatusDto?> Handle(GetOrderByTrackingCodeQuery request, CancellationToken cancellationToken)
    {
        var trackingCode = request.TrackingCode.Trim();
        if (string.IsNullOrWhiteSpace(trackingCode))
        {
            return null;
        }

        var order = await _dbContext.Orders
            .AsNoTracking()
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.TrackingCode == trackingCode, cancellationToken);

        if (order is null)
        {
            return null;
        }

        var fulfillmentStatus = order.FulfillmentStatus.ToString();

        var siteSettings = await _dbContext.SiteSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);
        var pickupSettings = PickupSettingsSnapshot.From(siteSettings);
        var now = _clock.UtcNow;
        var canChange = PickupScheduleCalculator.IsWithinChangeWindow(order, pickupSettings, now);
        var blockedReason = PickupScheduleCalculator.BuildChangeBlockedReason(order, pickupSettings, now);
        var estimatedReadyAt = order.ReadyAt
            ?? order.DeliveryAt
            ?? order.CreatedAt.AddMinutes(pickupSettings.MinLeadMinutes);

        return new OrderStatusDto(
            order.TrackingCode,
            order.Status.ToString(),
            fulfillmentStatus,
            FulfillmentStatusLabels.ToPersian(fulfillmentStatus, order.Status.ToString()),
            order.TotalAmount,
            order.CreatedAt,
            order.PaidAt,
            order.ReadyAt,
            order.Lines
                .Select(line => new OrderStatusLineDto(
                    line.PersianName,
                    line.EnglishName,
                    line.SizeLabel,
                    line.Quantity,
                    line.LineTotal,
                    line.ModifierLabels))
                .ToArray(),
            order.Id,
            order.DeliveryAt,
            estimatedReadyAt,
            order.DeliveryAt is not null,
            canChange,
            canChange && pickupSettings.Enabled,
            blockedReason,
            pickupSettings.RefundPolicyNote,
            pickupSettings.CancelCutoffMinutes);
    }
}

public static class FulfillmentStatusLabels
{
    public static string ToPersian(string fulfillmentStatus, string paymentStatus)
    {
        if (paymentStatus is not ("Paid" or "PartiallyPaid"))
        {
            return paymentStatus switch
            {
                "PendingPayment" => "در انتظار پرداخت",
                "Failed" => "پرداخت ناموفق",
                "Cancelled" => "لغو شده",
                _ => paymentStatus,
            };
        }

        return fulfillmentStatus switch
        {
            "Registered" => "سفارش ثبت شده",
            "Preparing" => "در حال آماده‌سازی",
            "Ready" => "سفارش آماده — می‌توانید تحویل بگیرید",
            "Completed" => "تحویل داده شد",
            _ => "در حال پردازش",
        };
    }
}
