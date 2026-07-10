using Microsoft.EntityFrameworkCore;

namespace Sugarista.Application.Orders.Queries.GetAdminOrders;

public record GetAdminOrdersQuery : IRequest<IReadOnlyCollection<AdminOrderListItemDto>>;

public record AdminOrderListItemDto(
    Guid Id,
    string TrackingCode,
    string CustomerName,
    string CustomerPhone,
    int TotalAmount,
    string Status,
    DateTimeOffset CreatedAt,
    int LineCount,
    IReadOnlyCollection<AdminOrderLineDto> Lines);

public record AdminOrderLineDto(
    Guid Id,
    string PersianName,
    string EnglishName,
    string? SizeLabel,
    int Quantity,
    int UnitPrice,
    int LineTotal,
    IReadOnlyCollection<string> ModifierLabels);

public class GetAdminOrdersQueryHandler : IRequestHandler<GetAdminOrdersQuery, IReadOnlyCollection<AdminOrderListItemDto>>
{
    private readonly IApplicationDbContext _dbContext;

    public GetAdminOrdersQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<AdminOrderListItemDto>> Handle(GetAdminOrdersQuery request, CancellationToken cancellationToken)
    {
        var orders = await _dbContext.Orders
            .AsNoTracking()
            .Include(o => o.Lines)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(cancellationToken);

        return orders
            .Select(order => new AdminOrderListItemDto(
                order.Id,
                order.TrackingCode,
                order.CustomerName,
                order.CustomerPhone,
                order.TotalAmount,
                order.Status.ToString(),
                order.CreatedAt,
                order.Lines.Count,
                order.Lines
                    .Select(line => new AdminOrderLineDto(
                        line.Id,
                        line.PersianName,
                        line.EnglishName,
                        line.SizeLabel,
                        line.Quantity,
                        line.UnitPrice,
                        line.LineTotal,
                        line.ModifierLabels))
                    .ToArray()))
            .ToArray();
    }
}
