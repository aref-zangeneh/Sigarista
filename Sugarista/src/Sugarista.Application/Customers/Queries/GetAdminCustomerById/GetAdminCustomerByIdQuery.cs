using Microsoft.EntityFrameworkCore;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Customers.Queries.GetAdminCustomerById;

public record GetAdminCustomerByIdQuery(Guid Id) : IRequest<AdminCustomerDetailDto?>;

public record AdminCustomerDetailDto(
    Guid Id,
    string PersianName,
    string PhoneNumber,
    string? Email,
    string? Notes,
    string FirstChannel,
    string LastChannel,
    bool IsActive,
    int TotalOrders,
    int PaidOrders,
    decimal TotalPaidAmount,
    decimal TotalTax,
    decimal AverageOrderValue,
    DateTimeOffset? FirstOrderAt,
    DateTimeOffset? LastOrderAt,
    int LoyaltyPoints,
    IReadOnlyCollection<AdminCustomerOrderDto> RecentOrders);

public record AdminCustomerOrderDto(
    Guid OrderId,
    string TrackingCode,
    decimal TotalAmount,
    decimal TaxTotal,
    decimal DeliveryFee,
    string Status,
    string Channel,
    string? ServiceType,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PaidAt);

public class GetAdminCustomerByIdQueryHandler : IRequestHandler<GetAdminCustomerByIdQuery, AdminCustomerDetailDto?>
{
    private readonly IApplicationDbContext _dbContext;

    public GetAdminCustomerByIdQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AdminCustomerDetailDto?> Handle(
        GetAdminCustomerByIdQuery request,
        CancellationToken cancellationToken)
    {
        var customer = await _dbContext.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(entry => entry.Id == request.Id, cancellationToken);

        if (customer is null)
        {
            return null;
        }

        var recentOrders = await _dbContext.Orders
            .AsNoTracking()
            .Where(order => order.CustomerId == customer.Id)
            .OrderByDescending(order => order.CreatedAt)
            .Take(12)
            .Select(order => new AdminCustomerOrderDto(
                order.Id,
                order.TrackingCode,
                order.TotalAmount,
                order.TaxTotal,
                order.DeliveryFee,
                order.Status.ToString(),
                order.Channel.ToString(),
                order.ServiceType != null ? order.ServiceType.ToString() : null,
                order.CreatedAt,
                order.PaidAt))
            .ToListAsync(cancellationToken);

        var paidOrders = await _dbContext.Orders
            .AsNoTracking()
            .Where(order => order.CustomerId == customer.Id && order.Status == OrderStatus.Paid)
            .Select(order => new { order.TotalAmount, order.TaxTotal })
            .ToListAsync(cancellationToken);

        var totalPaid = paidOrders.Sum(entry => entry.TotalAmount);

        return new AdminCustomerDetailDto(
            customer.Id,
            customer.PersianName,
            customer.PhoneNumber,
            customer.Email,
            customer.Notes,
            customer.FirstChannel,
            customer.LastChannel,
            customer.IsActive,
            recentOrders.Count == 12
                ? await _dbContext.Orders.CountAsync(order => order.CustomerId == customer.Id, cancellationToken)
                : recentOrders.Count,
            paidOrders.Count,
            totalPaid,
            paidOrders.Sum(entry => entry.TaxTotal),
            paidOrders.Count > 0 ? totalPaid / paidOrders.Count : 0m,
            recentOrders.Count > 0 ? recentOrders[^1].CreatedAt : null,
            recentOrders.Count > 0 ? recentOrders[0].CreatedAt : null,
            (int)(totalPaid / 10000m),
            recentOrders);
    }
}
