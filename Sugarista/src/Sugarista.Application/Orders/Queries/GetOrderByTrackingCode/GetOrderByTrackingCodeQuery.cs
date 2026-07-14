using Microsoft.EntityFrameworkCore;

namespace Sugarista.Application.Orders.Queries.GetOrderByTrackingCode;

public record GetOrderByTrackingCodeQuery(string TrackingCode) : IRequest<OrderStatusDto?>;

public record OrderStatusDto(
    Guid Id,
    string TrackingCode,
    string CustomerName,
    string Status,
    decimal TotalAmount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PaidAt,
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

        return new OrderStatusDto(
            order.Id,
            order.TrackingCode,
            order.CustomerName,
            order.Status.ToString(),
            order.TotalAmount,
            order.CreatedAt,
            order.PaidAt,
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
