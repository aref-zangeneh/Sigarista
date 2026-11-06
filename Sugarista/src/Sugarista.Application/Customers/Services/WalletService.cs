using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Customers.Services;

/// <summary>
/// Single entry point for every wallet movement: each mutation credits or
/// debits the wallet through the domain and writes one immutable
/// <see cref="WalletTransaction"/> — so the full history is always derivable
/// from the log and the balance can never silently go negative.
/// </summary>
public static class WalletService
{
    public static async Task<CustomerWallet> GetOrCreateAsync(
        IApplicationDbContext dbContext,
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        var wallet = await dbContext.CustomerWallets
            .FirstOrDefaultAsync(entry => entry.CustomerId == customerId, cancellationToken);

        if (wallet is not null)
        {
            return wallet;
        }

        wallet = new CustomerWallet(customerId);
        dbContext.CustomerWallets.Add(wallet);
        await dbContext.SaveChangesAsync(cancellationToken);
        return wallet;
    }

    /// <summary>Charges money in via the POS; logged as TopUp.</summary>
    public static async Task<CustomerWallet> TopUpAsync(
        IApplicationDbContext dbContext,
        CustomerWallet wallet,
        decimal amount,
        string description,
        CancellationToken cancellationToken = default)
    {
        wallet.Credit(amount);
        dbContext.WalletTransactions.Add(new WalletTransaction(
            wallet.Id,
            WalletTransactionType.TopUp,
            amount,
            wallet.Balance,
            description));
        await dbContext.SaveChangesAsync(cancellationToken);
        return wallet;
    }

    /// <summary>Admin correction; logged as Adjustment (signed amount, either direction).</summary>
    public static async Task<CustomerWallet> AdjustAsync(
        IApplicationDbContext dbContext,
        CustomerWallet wallet,
        decimal signedAmount,
        string description,
        CancellationToken cancellationToken = default)
    {
        if (signedAmount < 0)
        {
            wallet.Debit(-signedAmount);
        }
        else
        {
            wallet.Credit(signedAmount);
        }

        dbContext.WalletTransactions.Add(new WalletTransaction(
            wallet.Id,
            WalletTransactionType.Adjustment,
            signedAmount,
            wallet.Balance,
            description));
        await dbContext.SaveChangesAsync(cancellationToken);
        return wallet;
    }

    /// <summary>
    /// Pays an order (or part of it) from the wallet. The wallet is debited
    /// first and exactly once; the caller then reduces the order payable by
    /// the same amount. Logged as Purchase for the order.
    /// </summary>
    public static async Task<decimal> SpendForOrderAsync(
        IApplicationDbContext dbContext,
        CustomerWallet wallet,
        Order order,
        decimal payable,
        int paymentMaxPercent = 100,
        CancellationToken cancellationToken = default)
    {
        if (payable <= 0)
        {
            return 0m;
        }

        // Idempotency: never spend twice for the same order.
        var alreadySpent = await dbContext.WalletTransactions
            .AnyAsync(transaction => transaction.OrderId == order.Id && transaction.Type == WalletTransactionType.Purchase, cancellationToken);
        if (alreadySpent)
        {
            return 0m;
        }

        var amount = Math.Min(wallet.Balance, payable);

        // سقف پرداخت از کیف پول: بیش از این درصدِ هر سفارش از کیف پول کسر نمی‌شود.
        if (paymentMaxPercent < 100)
        {
            amount = Math.Min(amount, Math.Floor(payable * paymentMaxPercent / 100m));
        }

        if (amount <= 0)
        {
            return 0m;
        }

        wallet.Debit(amount);
        dbContext.WalletTransactions.Add(new WalletTransaction(
            wallet.Id,
            WalletTransactionType.Purchase,
            -amount,
            wallet.Balance,
            $"پرداخت سفارش {order.TrackingCode} با کیف پول",
            order.Id));
        await dbContext.SaveChangesAsync(cancellationToken);
        return amount;
    }

    /// <summary>Percent cashback after a paid order; logged as Cashback.</summary>
    public static async Task AwardCashbackAsync(
        IApplicationDbContext dbContext,
        Sugarista.Domain.Entities.SiteSettings settings,
        Order order,
        Customer customer,
        CancellationToken cancellationToken = default)
    {
        if (!settings.WalletEnabled)
        {
            return;
        }

        // Tier-specific percent: the customer's current level may override
        // (including zeroing) the global cashback percent.
        var percent = settings.WalletCashbackPercent;
        if (customer.MembershipTierId is not null)
        {
            var tierPercent = await dbContext.MembershipTiers
                .AsNoTracking()
                .Where(tier => tier.Id == customer.MembershipTierId)
                .Select(tier => (decimal?)tier.CashbackPercent)
                .FirstOrDefaultAsync(cancellationToken);
            if (tierPercent.HasValue)
            {
                percent = tierPercent.Value;
            }
        }

        if (percent <= 0)
        {
            return;
        }

        // Idempotency: one cashback per order.
        var alreadyGranted = await dbContext.WalletTransactions
            .AnyAsync(transaction => transaction.OrderId == order.Id && transaction.Type == WalletTransactionType.Cashback, cancellationToken);
        if (alreadyGranted)
        {
            return;
        }

        // Cashback is granted on what the customer actually paid, in total —
        // this is the net amount after all discounts and wallet payments.
        var paidAmount = Math.Max(0m, order.TotalAmount);
        if (paidAmount < settings.WalletCashbackMinOrderAmount)
        {
            return;
        }

        var cashback = Math.Floor(paidAmount * percent / 100m);

        // سقف کش‌بک: بیش از این مبلغ برای یک سفارش کش‌بک نمی‌گیرد.
        if (settings.WalletCashbackMaxPerOrderAmount > 0)
        {
            cashback = Math.Min(cashback, settings.WalletCashbackMaxPerOrderAmount);
        }

        if (cashback <= 0)
        {
            return;
        }

        var expiresAt = settings.WalletCashbackExpiryDays > 0
            ? (DateTimeOffset?)DateTimeOffset.UtcNow.AddDays(settings.WalletCashbackExpiryDays)
            : null;

        var wallet = await GetOrCreateAsync(dbContext, customer.Id, cancellationToken);
        wallet.Credit(cashback);
        dbContext.WalletTransactions.Add(new WalletTransaction(
            wallet.Id,
            WalletTransactionType.Cashback,
            cashback,
            wallet.Balance,
            $"کش‌بک سفارش {order.TrackingCode} ({percent:0.##}٪)",
            order.Id,
            expiresAt));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Logs expired cashback out of the wallet as its own visible
    /// transaction. Runs before any spend or balance display so the customer
    /// always sees an honest balance; expired lots are only retired while the
    /// balance still covers them, so the balance can never dip below zero.
    /// </summary>
    public static async Task<int> ExpireDueCashbackAsync(
        IApplicationDbContext dbContext,
        CustomerWallet wallet,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        if (wallet.Balance <= 0)
        {
            return 0;
        }

        var dueTransactions = await dbContext.WalletTransactions
            .Where(transaction => transaction.WalletId == wallet.Id &&
                                  transaction.Type == WalletTransactionType.Cashback &&
                                  transaction.ExpiresAt != null &&
                                  transaction.ExpiresAt <= now)
            .OrderBy(transaction => transaction.ExpiresAt)
            .ToListAsync(cancellationToken);

        var expiredCount = 0;
        foreach (var transaction in dueTransactions)
        {
            var amount = transaction.Amount;
            if (amount <= 0 || wallet.Balance < amount)
            {
                // Already spent most of the balance — keep the lot pending
                // until the balance can cover its retirement honestly.
                continue;
            }

            wallet.Debit(amount);
            dbContext.WalletTransactions.Add(new WalletTransaction(
                wallet.Id,
                WalletTransactionType.Adjustment,
                -amount,
                wallet.Balance,
                $"انقضای کش‌بک ({transaction.Description})"));
            // Retire the lot: clearing the marker makes the sweep idempotent.
            transaction.MarkExpiryLogged();
            expiredCount++;
        }

        if (expiredCount > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return expiredCount;
    }

    /// <summary>Returns the wallet spend of a paid order when it is cancelled; logged as Refund.</summary>
    public static async Task RefundCancelledOrderAsync(
        IApplicationDbContext dbContext,
        Order order,
        CancellationToken cancellationToken = default)
    {
        var purchaseTransactions = await dbContext.WalletTransactions
            .Include(transaction => transaction.Wallet)
            .Where(transaction => transaction.OrderId == order.Id && transaction.Type == WalletTransactionType.Purchase)
            .ToListAsync(cancellationToken);

        var alreadyRefunded = await dbContext.WalletTransactions
            .AnyAsync(transaction => transaction.OrderId == order.Id && transaction.Type == WalletTransactionType.Refund, cancellationToken);

        if (purchaseTransactions.Count == 0 || alreadyRefunded)
        {
            return;
        }

        var refundTotal = purchaseTransactions.Sum(transaction => -transaction.Amount);
        if (refundTotal <= 0)
        {
            return;
        }

        // All purchases belong to one wallet — credit it once, no matter how
        // many purchase lines were written for the order.
        foreach (var walletGroup in purchaseTransactions.GroupBy(transaction => transaction.WalletId))
        {
            var wallet = walletGroup.First().Wallet;
            wallet.Credit(refundTotal);
            dbContext.WalletTransactions.Add(new WalletTransaction(
                walletGroup.Key,
                WalletTransactionType.Refund,
                refundTotal,
                wallet.Balance,
                $"برگشت هزینهٔ کیف پول سفارش لغو شده {order.TrackingCode}",
                order.Id));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
