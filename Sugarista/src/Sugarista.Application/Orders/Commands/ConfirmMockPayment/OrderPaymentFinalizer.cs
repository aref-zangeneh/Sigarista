using Sugarista.Application.Accounting.Services;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.Customers.Services;
using Sugarista.Application.DeliveryMethods.Services;
using Sugarista.Application.Orders.Services;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Orders.Commands.ConfirmMockPayment;

/// <summary>
/// One shared pipeline for "the order just became fully paid": inventory
/// deduction, accounting entry, receipt printing and the loyalty/wallet award
/// chain (points, stamp cards, badges, membership tier, cashback). Used by the
/// mock payment confirmation and by checkout when the wallet covers the whole
/// order — the customer-visible side-effects stay identical on both paths.
/// </summary>
public interface IOrderPaymentFinalizer
{
    Task FinalizePaidOrderAsync(Order order, CancellationToken cancellationToken = default);
}

public class OrderPaymentFinalizer : IOrderPaymentFinalizer
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IAccountingService _accountingService;
    private readonly IPrintJobService _printJobService;
    private readonly ILoyaltyService _loyaltyService;

    public OrderPaymentFinalizer(
        IApplicationDbContext dbContext,
        IAccountingService accountingService,
        IPrintJobService printJobService,
        ILoyaltyService loyaltyService)
    {
        _dbContext = dbContext;
        _accountingService = accountingService;
        _printJobService = printJobService;
        _loyaltyService = loyaltyService;
    }

    public async Task FinalizePaidOrderAsync(Order order, CancellationToken cancellationToken = default)
    {
        order.MarkPaid();

        await OrderInventoryService.DeductInventoryForOrderLinesAsync(
            _dbContext,
            order,
            order.Lines.ToList(),
            cancellationToken);
        await DeliveryMethodInventoryService.DeductForOrderAsync(_dbContext, order, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _accountingService.PostOrderPaymentAsync(order, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Queue the thermal receipt copies (customer + barista) for the print agent.
        await _printJobService.EnqueueOrderReceiptsAsync(order, cancellationToken);

        // Loyalty award chain: points, stamp cards, badges, membership tier.
        await _loyaltyService.AwardForPaidOrderAsync(order, cancellationToken);
    }
}
