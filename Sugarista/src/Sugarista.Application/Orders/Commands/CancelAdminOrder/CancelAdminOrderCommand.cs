using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Orders.Commands.UpdateAdminOrder;
using Sugarista.Application.Orders.Services;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Orders.Commands.CancelAdminOrder;

public record CancelAdminOrderCommand(Guid OrderId) : IRequest<AdminOrderDetailDto>;

public class CancelAdminOrderCommandValidator : AbstractValidator<CancelAdminOrderCommand>
{
    public CancelAdminOrderCommandValidator()
    {
        RuleFor(c => c.OrderId).NotEmpty();
    }
}

public class CancelAdminOrderCommandHandler : IRequestHandler<CancelAdminOrderCommand, AdminOrderDetailDto>
{
    private readonly IApplicationDbContext _dbContext;

    public CancelAdminOrderCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AdminOrderDetailDto> Handle(CancelAdminOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await _dbContext.Orders
            .Include(o => o.Lines)
            .Include(o => o.Payments)
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken)
            ?? throw new KeyNotFoundException("Order was not found.");

        if (order.Status == OrderStatus.Cancelled)
        {
            return AdminOrderMapper.ToDetail(order);
        }

        await OrderInventoryService.RestoreInventoryForOrderLinesAsync(
            _dbContext,
            order,
            order.Lines.ToList(),
            cancellationToken);

        foreach (var payment in order.Payments.Where(payment => payment.Status == OrderPaymentStatus.PendingPayment))
        {
            payment.MarkCancelled();
        }

        order.AdminCancel();
        await _dbContext.SaveChangesAsync(cancellationToken);

        return AdminOrderMapper.ToDetail(order);
    }
}
