using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Wallets.Queries.GetAdminWalletSummary;

public record AdminWalletSettingsDto(
    bool WalletEnabled,
    decimal WalletCashbackPercent,
    decimal WalletCashbackMinOrderAmount,
    decimal WalletCashbackMaxPerOrderAmount,
    int WalletCashbackExpiryDays,
    int WalletPaymentMaxPercent,
    string PickupRefundMode);

public record AdminWalletSummaryDto(
    decimal TotalBalance,
    int TotalWalletsCount,
    int ActiveWalletsCount,
    int TotalCustomersCount,
    decimal TotalTopUpAmount,
    decimal TotalCashbackAmount,
    decimal TotalPurchaseAmount,
    decimal TotalRefundAmount,
    decimal NetAdjustmentAmount,
    decimal ExpiringSoonCashbackAmount,
    int DueExpiredCashbackCount,
    int TotalTransactionsCount,
    AdminWalletSettingsDto Settings);

public record GetAdminWalletSummaryQuery : IRequest<AdminWalletSummaryDto>;

public class GetAdminWalletSummaryQueryHandler : IRequestHandler<GetAdminWalletSummaryQuery, AdminWalletSummaryDto>
{
    private readonly IApplicationDbContext _dbContext;

    public GetAdminWalletSummaryQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AdminWalletSummaryDto> Handle(GetAdminWalletSummaryQuery request, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var soonCutoff = now.AddDays(7);

        var totalCustomersCount = await _dbContext.Customers
            .AsNoTracking()
            .CountAsync(cancellationToken);

        var walletsQuery = _dbContext.CustomerWallets.AsNoTracking();
        var totalWalletsCount = await walletsQuery.CountAsync(cancellationToken);
        var activeWalletsCount = await walletsQuery.CountAsync(w => w.Balance > 0m, cancellationToken);
        var totalBalance = await walletsQuery.SumAsync(w => (decimal?)w.Balance, cancellationToken) ?? 0m;

        var txQuery = _dbContext.WalletTransactions.AsNoTracking();
        var totalTransactionsCount = await txQuery.CountAsync(cancellationToken);

        var groupedTotals = await txQuery
            .GroupBy(t => t.Type)
            .Select(g => new { Type = g.Key, Sum = g.Sum(x => x.Amount) })
            .ToListAsync(cancellationToken);

        decimal SumFor(WalletTransactionType type) =>
            groupedTotals.FirstOrDefault(x => x.Type == type)?.Sum ?? 0m;

        var totalTopUp = SumFor(WalletTransactionType.TopUp);
        var totalCashback = SumFor(WalletTransactionType.Cashback);
        var totalPurchase = Math.Abs(SumFor(WalletTransactionType.Purchase));
        var totalRefund = SumFor(WalletTransactionType.Refund);
        var netAdjustment = SumFor(WalletTransactionType.Adjustment);

        var expiringSoonCashback = await txQuery
            .Where(t => t.Type == WalletTransactionType.Cashback &&
                        t.ExpiresAt != null &&
                        t.ExpiresAt > now &&
                        t.ExpiresAt <= soonCutoff)
            .SumAsync(t => (decimal?)t.Amount, cancellationToken) ?? 0m;

        var dueExpiredCount = await txQuery
            .Where(t => t.Type == WalletTransactionType.Cashback &&
                        t.ExpiresAt != null &&
                        t.ExpiresAt <= now)
            .CountAsync(cancellationToken);

        var settings = await _dbContext.SiteSettings
            .AsNoTracking()
            .OrderBy(s => s.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var settingsDto = new AdminWalletSettingsDto(
            settings?.WalletEnabled ?? true,
            settings?.WalletCashbackPercent ?? 2m,
            settings?.WalletCashbackMinOrderAmount ?? 0m,
            settings?.WalletCashbackMaxPerOrderAmount ?? 0m,
            settings?.WalletCashbackExpiryDays ?? 0,
            settings?.WalletPaymentMaxPercent ?? 100,
            settings?.PickupRefundMode ?? "Wallet");

        return new AdminWalletSummaryDto(
            totalBalance,
            totalWalletsCount,
            activeWalletsCount,
            totalCustomersCount,
            totalTopUp,
            totalCashback,
            totalPurchase,
            totalRefund,
            netAdjustment,
            expiringSoonCashback,
            dueExpiredCount,
            totalTransactionsCount,
            settingsDto);
    }
}
