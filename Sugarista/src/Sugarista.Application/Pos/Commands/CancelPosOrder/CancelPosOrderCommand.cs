using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Affiliates.Services;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.DeliveryMethods.Services;
using Sugarista.Application.Orders.Services;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Pos.Commands.CancelPosOrder;

public record CancelPosOrderCommand(Guid OrderId) : IRequest;

public class CancelPosOrderCommandHandler : IRequestHandler<CancelPosOrderCommand>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IAffiliateCommissionLifecycleService? _affiliateLifecycleService;

    public CancelPosOrderCommandHandler(
        IApplicationDbContext dbContext,
        IAffiliateCommissionLifecycleService? affiliateLifecycleService = null)
    {
        _dbContext = dbContext;
        _affiliateLifecycleService = affiliateLifecycleService;
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

        await DeliveryMethodInventoryService.RestoreForOrderAsync(_dbContext, order, cancellationToken);

        order.AdminCancel();
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (_affiliateLifecycleService is not null)
        {
            await _affiliateLifecycleService.SyncForOrderAsync(order, "سفارش صندوق لغو شد.", cancellationToken);
        }
    }
}
