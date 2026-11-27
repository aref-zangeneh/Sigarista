using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Accounting.Services;
using Sugarista.Application.Affiliates.Services;
using Sugarista.Application.Customers.Services;
using Sugarista.Application.DeliveryMethods.Services;
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
    private readonly ILoyaltyService _loyaltyService;
    private readonly IAccountingService? _accountingService;
    private readonly IAffiliateCommissionLifecycleService? _affiliateLifecycleService;

    public CancelAdminOrderCommandHandler(
        IApplicationDbContext dbContext,
        ILoyaltyService loyaltyService,
        IAccountingService? accountingService = null,
        IAffiliateCommissionLifecycleService? affiliateLifecycleService = null)
    {
        _dbContext = dbContext;
        _loyaltyService = loyaltyService;
        _accountingService = accountingService;
        _affiliateLifecycleService = affiliateLifecycleService;
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

        await DeliveryMethodInventoryService.RestoreForOrderAsync(_dbContext, order, cancellationToken);

        foreach (var payment in order.Payments.Where(payment => payment.Status == OrderPaymentStatus.PendingPayment))
        {
            payment.MarkCancelled();
        }

        order.AdminCancel();
        if (_accountingService is not null)
        {
            await _accountingService.RemoveOrderJournalEntriesAsync(order.Id, cancellationToken);
        }
        await _loyaltyService.RefundForCancelledOrderAsync(order, cancellationToken);
        await WalletService.RefundCancelledOrderAsync(_dbContext, order, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (_affiliateLifecycleService is not null)
        {
            await _affiliateLifecycleService.SyncForOrderAsync(order, "سفارش توسط ادمین لغو شد.", cancellationToken);
        }

        return AdminOrderMapper.ToDetail(order);
    }
}
