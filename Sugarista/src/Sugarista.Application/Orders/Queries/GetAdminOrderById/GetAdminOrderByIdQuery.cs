using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Orders.Commands.UpdateAdminOrder;

namespace Sugarista.Application.Orders.Queries.GetAdminOrderById;

public record GetAdminOrderByIdQuery(Guid OrderId) : IRequest<AdminOrderDetailDto?>;

public class GetAdminOrderByIdQueryHandler : IRequestHandler<GetAdminOrderByIdQuery, AdminOrderDetailDto?>
{
    private readonly IApplicationDbContext _dbContext;

    public GetAdminOrderByIdQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AdminOrderDetailDto?> Handle(GetAdminOrderByIdQuery request, CancellationToken cancellationToken)
    {
        var order = await _dbContext.Orders
            .AsNoTracking()
            .Include(o => o.Lines)
            .Include(o => o.Payments)
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken);

        return order is null ? null : AdminOrderMapper.ToDetail(order);
    }
}
