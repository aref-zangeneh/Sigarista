using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Extensions;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Customers.Queries.GetAdminCustomers;

public record GetAdminCustomersQuery(int Page = 1, int PageSize = 25, string? Search = null)
    : IRequest<PaginatedList<AdminCustomerListItemDto>>;

public record AdminCustomerListItemDto(
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
    int LoyaltyPoints);

public class GetAdminCustomersQueryHandler : IRequestHandler<GetAdminCustomersQuery, PaginatedList<AdminCustomerListItemDto>>
{
    private readonly IApplicationDbContext _dbContext;

    public GetAdminCustomersQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PaginatedList<AdminCustomerListItemDto>> Handle(
        GetAdminCustomersQuery request,
        CancellationToken cancellationToken)
    {
        var pagination = new PaginationQuery(request.Page, request.PageSize, request.Search);

        var query = _dbContext.Customers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(pagination.Search))
        {
            var search = pagination.Search.Trim().ToLowerInvariant();
            query = query.Where(customer =>
                customer.PersianName.ToLower().Contains(search) ||
                customer.PhoneNumber.Contains(search) ||
                (customer.Email ?? string.Empty).ToLower().Contains(search));
        }

        return await query
            .OrderByDescending(customer => customer.Orders.Count(order => order.Status == OrderStatus.Paid))
            .ThenBy(customer => customer.PersianName)
            .Select(customer => new AdminCustomerListItemDto(
                customer.Id,
                customer.PersianName,
                customer.PhoneNumber,
                customer.Email,
                customer.Notes,
                customer.FirstChannel,
                customer.LastChannel,
                customer.IsActive,
                customer.Orders.Count,
                customer.Orders.Count(order => order.Status == OrderStatus.Paid),
                customer.Orders.Where(order => order.Status == OrderStatus.Paid).Sum(order => order.TotalAmount),
                customer.Orders.Where(order => order.Status == OrderStatus.Paid).Sum(order => order.TaxTotal),
                customer.Orders.Any(order => order.Status == OrderStatus.Paid)
                    ? customer.Orders.Where(order => order.Status == OrderStatus.Paid).Average(order => order.TotalAmount)
                    : 0m,
                customer.Orders.Select(order => (DateTimeOffset?)order.CreatedAt).DefaultIfEmpty().Min(),
                customer.Orders.Select(order => (DateTimeOffset?)order.CreatedAt).DefaultIfEmpty().Max(),
                (int)(customer.Orders.Where(order => order.Status == OrderStatus.Paid).Sum(order => order.TotalAmount) / 10000m)))
            .ToPaginatedListAsync(pagination, cancellationToken);
    }
}
