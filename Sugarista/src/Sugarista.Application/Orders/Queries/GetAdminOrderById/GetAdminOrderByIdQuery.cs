using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.Orders.Commands.UpdateAdminOrder;
using Sugarista.Domain.Constants;

namespace Sugarista.Application.Orders.Queries.GetAdminOrderById;

public record GetAdminOrderByIdQuery(Guid OrderId) : IRequest<AdminOrderDetailDto?>;

public class GetAdminOrderByIdQueryHandler : IRequestHandler<GetAdminOrderByIdQuery, AdminOrderDetailDto?>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public GetAdminOrderByIdQueryHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<AdminOrderDetailDto?> Handle(GetAdminOrderByIdQuery request, CancellationToken cancellationToken)
    {
        var order = await _dbContext.Orders
            .AsNoTracking()
            .Include(o => o.Lines)
            .Include(o => o.Payments)
            .Include(o => o.Discounts)
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken);

        var includeManagerNote = _currentUserService.HasPermission(AdminPermissions.Pos.ManagerNote);
        return order is null ? null : AdminOrderMapper.ToDetail(order, includeManagerNote);
    }
}
