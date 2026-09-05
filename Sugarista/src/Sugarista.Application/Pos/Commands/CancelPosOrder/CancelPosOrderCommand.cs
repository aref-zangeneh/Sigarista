using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.Orders.Services;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Pos.Commands.CancelPosOrder;

public record CancelPosOrderCommand(Guid OrderId) : IRequest;

public class CancelPosOrderCommandHandler : IRequestHandler<CancelPosOrderCommand>
{
    private readonly IApplicationDbContext _dbContext;

    public CancelPosOrderCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task Handle(CancelPosOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await _dbContext.Orders
            .Include(entry => entry.Lines)
            .FirstOrDefaultAsync(entry => entry.Id == request.OrderId, cancellationToken)
            ?? throw new KeyNotFoundException("Order was not found.");

        if (order.Channel != OrderChannel.Pos)
        {
            throw new InvalidOperationException("Only POS orders can be cancelled here.");
        }

        if (order.Status == OrderStatus.Paid)
        {
            throw new InvalidOperationException("Paid orders cannot be cancelled from POS.");
        }

        if (order.Lines.Any(line => line.InventoryDeductedQuantity > 0))
        {
            await OrderInventoryService.RestoreInventoryForOrderLinesAsync(
                _dbContext,
                order,
                order.Lines.ToList(),
                cancellationToken);
        }

        order.AdminCancel();
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
