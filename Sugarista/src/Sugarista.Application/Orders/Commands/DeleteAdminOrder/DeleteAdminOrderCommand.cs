using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Accounting.Services;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.Customers.Services;
using Sugarista.Application.DeliveryMethods.Services;
using Sugarista.Application.Orders.Services;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Orders.Commands.DeleteAdminOrder;

public record DeleteAdminOrderCommand(Guid OrderId, bool RestoreInventory = true) : IRequest;

public class DeleteAdminOrderCommandValidator : AbstractValidator<DeleteAdminOrderCommand>
{
    public DeleteAdminOrderCommandValidator()
    {
        RuleFor(c => c.OrderId).NotEmpty();
    }
}

public class DeleteAdminOrderCommandHandler : IRequestHandler<DeleteAdminOrderCommand>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IAccountingService _accountingService;
    private readonly ILoyaltyService _loyaltyService;

    public DeleteAdminOrderCommandHandler(
        IApplicationDbContext dbContext,
        IAccountingService accountingService,
        ILoyaltyService loyaltyService)
    {
        _dbContext = dbContext;
        _accountingService = accountingService;
        _loyaltyService = loyaltyService;
    }

    public async Task Handle(DeleteAdminOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await _dbContext.Orders
            .Include(o => o.Lines)
            .Include(o => o.Payments)
            .Include(o => o.SplitShares)
            .Include(o => o.Discounts)
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken)
            ?? throw new KeyNotFoundException("Order was not found.");

        if (order.Status != OrderStatus.Cancelled)
        {
            if (request.RestoreInventory && order.Lines.Any(line => line.InventoryDeductedQuantity > 0))
            {
                await OrderInventoryService.RestoreInventoryForOrderLinesAsync(
                    _dbContext,
                    order,
                    order.Lines.ToList(),
                    cancellationToken);
            }

            if (request.RestoreInventory)
            {
                await DeliveryMethodInventoryService.RestoreForOrderAsync(_dbContext, order, cancellationToken);
            }

            await _loyaltyService.RefundForCancelledOrderAsync(order, cancellationToken);
            await WalletService.RefundCancelledOrderAsync(_dbContext, order, cancellationToken);
        }

        await _accountingService.RemoveOrderJournalEntriesAsync(order.Id, cancellationToken);

        var redemptions = await _dbContext.DiscountRedemptions
            .Where(redemption => redemption.OrderId == order.Id)
            .ToListAsync(cancellationToken);
        if (redemptions.Count > 0)
        {
            _dbContext.DiscountRedemptions.RemoveRange(redemptions);
        }

        var printJobs = await _dbContext.PrintJobs
            .Where(job => job.OrderId == order.Id)
            .ToListAsync(cancellationToken);
        if (printJobs.Count > 0)
        {
            _dbContext.PrintJobs.RemoveRange(printJobs);
        }

        _dbContext.Orders.Remove(order);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
