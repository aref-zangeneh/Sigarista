using Microsoft.EntityFrameworkCore;

namespace Sugarista.Application.Orders.Queries.GetOrderByTrackingCode;

public record GetOrderByTrackingCodeQuery(string TrackingCode) : IRequest<OrderStatusDto?>;

public record OrderStatusDto(
    Guid Id,
    string TrackingCode,
    string CustomerName,
    string Status,
    string FulfillmentStatus,
    string FulfillmentStatusLabel,
    decimal TotalAmount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PaidAt,
    DateTimeOffset? ReadyAt,
    IReadOnlyCollection<OrderStatusLineDto> Lines);

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

    public GetOrderByTrackingCodeQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
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

        return new OrderStatusDto(
            order.Id,
            order.TrackingCode,
            order.CustomerName,
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
                .ToArray());
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
