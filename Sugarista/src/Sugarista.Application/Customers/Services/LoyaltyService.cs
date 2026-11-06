using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Customers.Services;

public interface ILoyaltyService
{
    Task AwardForPaidOrderAsync(Order order, CancellationToken cancellationToken = default);

    Task RefundForCancelledOrderAsync(Order order, CancellationToken cancellationToken = default);
}

public class LoyaltyService : ILoyaltyService
{
    private readonly IApplicationDbContext _dbContext;

    public LoyaltyService(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AwardForPaidOrderAsync(Order order, CancellationToken cancellationToken = default)
    {
        var settings = await _dbContext.SiteSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        if (settings is null)
        {
            return;
        }

        var customer = await ResolveCustomerAsync(order, cancellationToken);
        if (customer is null)
        {
            return;
        }

        await AwardLoyaltyPointsAsync(settings, order, customer, cancellationToken);
        await StampCardService.AwardCardsAsync(_dbContext, settings, order, customer, cancellationToken);
        await BadgeService.EvaluateForCustomerAsync(_dbContext, customer, cancellationToken);
        await MembershipTierService.EvaluateForCustomerAsync(_dbContext, customer, cancellationToken);
        await WalletService.AwardCashbackAsync(_dbContext, settings, order, customer, cancellationToken);
    }

    private async Task AwardLoyaltyPointsAsync(
        Sugarista.Domain.Entities.SiteSettings settings,
        Order order,
        Customer customer,
        CancellationToken cancellationToken)
    {
        if (!settings.LoyaltyEnabled || settings.LoyaltyEarnTomanPerPoint <= 0)
        {
            return;
        }

        // Idempotency: don't award twice for the same order.
        var alreadyAwarded = await _dbContext.LoyaltyTransactions
            .AnyAsync(transaction => transaction.OrderId == order.Id && transaction.Type == LoyaltyTransactionType.Earn, cancellationToken);
        if (alreadyAwarded)
        {
            return;
        }

        var points = Math.Floor(order.TotalAmount / settings.LoyaltyEarnTomanPerPoint);
        if (points <= 0)
        {
            return;
        }

        DateTimeOffset? expiresAt = settings.LoyaltyPointsExpiryDays > 0
            ? DateTimeOffset.UtcNow.AddDays(settings.LoyaltyPointsExpiryDays)
            : null;

        customer.AddLoyaltyPoints(points);
        _dbContext.LoyaltyTransactions.Add(new LoyaltyTransaction(
            customer.Id,
            LoyaltyTransactionType.Earn,
            points,
            customer.LoyaltyPoints,
            $"امتیاز سفارش {order.TrackingCode}",
            order.Id,
            expiresAt));

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RefundForCancelledOrderAsync(Order order, CancellationToken cancellationToken = default)
    {
        var earnTransaction = await _dbContext.LoyaltyTransactions
            .FirstOrDefaultAsync(transaction =>
                transaction.OrderId == order.Id && transaction.Type == LoyaltyTransactionType.Earn, cancellationToken);

        if (earnTransaction is null)
        {
            return;
        }

        // Idempotency: don't refund twice.
        var alreadyRefunded = await _dbContext.LoyaltyTransactions
            .AnyAsync(transaction => transaction.OrderId == order.Id && transaction.Type == LoyaltyTransactionType.Refund, cancellationToken);
        if (alreadyRefunded)
        {
            return;
        }

        var customer = await _dbContext.Customers.FirstOrDefaultAsync(c => c.Id == earnTransaction.CustomerId, cancellationToken);
        if (customer is null)
        {
            return;
        }

        var pointsToRefund = Math.Min(earnTransaction.Points, customer.LoyaltyPoints);
        if (pointsToRefund <= 0)
        {
            return;
        }

        customer.DeductLoyaltyPoints(pointsToRefund);
        _dbContext.LoyaltyTransactions.Add(new LoyaltyTransaction(
            customer.Id,
            LoyaltyTransactionType.Refund,
            -pointsToRefund,
            customer.LoyaltyPoints,
            $"برگشت امتیاز سفارش لغو شده {order.TrackingCode}",
            order.Id,
            null));

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<Customer?> ResolveCustomerAsync(Order order, CancellationToken cancellationToken)
    {
        if (order.CustomerId.HasValue)
        {
            return await _dbContext.Customers.FirstOrDefaultAsync(customer => customer.Id == order.CustomerId.Value, cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(order.CustomerPhone))
        {
            return null;
        }

        return await _dbContext.Customers.FirstOrDefaultAsync(customer => customer.PhoneNumber == order.CustomerPhone, cancellationToken);
    }
}
