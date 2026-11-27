using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Accounting.Services;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Affiliates.Services;

public sealed record AffiliateCommissionSyncResult(
    int ApprovedCount,
    int RejectedCount);

public interface IAffiliateCommissionLifecycleService
{
    /// <summary>
    /// Synchronizes AffiliateCommission status with the order's lifecycle:
    /// - Paid + FulfillmentStatus.Completed => Pending -> Approved + credits CashBalance + evaluates tier.
    /// - Cancelled / Failed => Pending/Approved -> Rejected (+ reverses CashBalance if previously Approved) + evaluates tier.
    /// </summary>
    Task<AffiliateCommissionSyncResult> SyncForOrderAsync(
        Order order,
        string? cancellationReason = null,
        CancellationToken cancellationToken = default);
}

public sealed class AffiliateCommissionLifecycleService : IAffiliateCommissionLifecycleService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IAffiliateTierEngine _tierEngine;
    private readonly IAffiliateNotificationService? _notificationService;
    private readonly IAccountingService? _accountingService;

    public AffiliateCommissionLifecycleService(
        IApplicationDbContext dbContext,
        IAffiliateTierEngine tierEngine,
        IAffiliateNotificationService? notificationService = null,
        IAccountingService? accountingService = null)
    {
        _dbContext = dbContext;
        _tierEngine = tierEngine;
        _notificationService = notificationService;
        _accountingService = accountingService;
    }

    public async Task<AffiliateCommissionSyncResult> SyncForOrderAsync(
        Order order,
        string? cancellationReason = null,
        CancellationToken cancellationToken = default)
    {
        var shouldApprove = order.Status == OrderStatus.Paid &&
                            order.FulfillmentStatus == FulfillmentStatus.Completed;

        var shouldReject = order.Status is OrderStatus.Cancelled or OrderStatus.Failed;

        if (!shouldApprove && !shouldReject)
        {
            return new AffiliateCommissionSyncResult(0, 0);
        }

        var commissions = await _dbContext.AffiliateCommissions
            .Include(c => c.Affiliate)
                .ThenInclude(a => a.User)
            .Where(c => c.OrderId == order.Id)
            .ToListAsync(cancellationToken);

        if (commissions.Count == 0)
        {
            return new AffiliateCommissionSyncResult(0, 0);
        }

        var approvedCount = 0;
        var rejectedCount = 0;
        var touchedAffiliateIds = new HashSet<Guid>();
        var approvedCommissions = new List<AffiliateCommission>();
        var reversedCommissionIds = new List<Guid>();
        var approvedToNotify = new List<(string? Phone, string Name, decimal Commission, decimal CashBalance)>();

        if (shouldApprove)
        {
            foreach (var commission in commissions.Where(c => c.Status == AffiliateCommissionStatus.Pending))
            {
                // Always approve the immutable order-time locked CommissionAmount (never recalculate).
                commission.Approve();
                if (commission.CommissionAmount > 0m)
                {
                    commission.Affiliate.CreditCashBalance(commission.CommissionAmount);
                    approvedCommissions.Add(commission);
                    approvedToNotify.Add((
                        commission.Affiliate.User?.PhoneNumber,
                        commission.Affiliate.User?.PersianName ?? "همکار گرامی",
                        commission.CommissionAmount,
                        commission.Affiliate.CashBalance));
                }

                approvedCount++;
                touchedAffiliateIds.Add(commission.AffiliateId);
            }
        }
        else if (shouldReject)
        {
            var reason = string.IsNullOrWhiteSpace(cancellationReason)
                ? "سفارش لغو یا مرجوع شد."
                : cancellationReason.Trim();

            foreach (var commission in commissions.Where(c =>
                         c.Status is AffiliateCommissionStatus.Pending or AffiliateCommissionStatus.Approved))
            {
                if (commission.Status == AffiliateCommissionStatus.Approved && commission.CommissionAmount > 0m)
                {
                    var reversible = Math.Min(commission.Affiliate.CashBalance, commission.CommissionAmount);
                    if (reversible > 0m)
                    {
                        commission.Affiliate.DebitCashBalance(reversible);
                    }
                    reversedCommissionIds.Add(commission.Id);
                }

                commission.Reject(reason);
                rejectedCount++;
                touchedAffiliateIds.Add(commission.AffiliateId);
            }
        }

        if (approvedCount > 0 || rejectedCount > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);

            if (_accountingService is not null)
            {
                foreach (var commission in approvedCommissions)
                {
                    await _accountingService.PostAffiliateCommissionApprovedAsync(
                        commission,
                        order.TrackingCode,
                        commission.Affiliate.User?.PersianName ?? "همکار فروش",
                        cancellationToken);
                }

                foreach (var reversedId in reversedCommissionIds)
                {
                    await _accountingService.ReverseAffiliateCommissionAsync(reversedId, cancellationToken);
                }
            }

            foreach (var affiliateId in touchedAffiliateIds)
            {
                await _tierEngine.EvaluateAffiliateTierAsync(affiliateId, cancellationToken: cancellationToken);
            }

            if (_notificationService is not null)
            {
                foreach (var item in approvedToNotify)
                {
                    await _notificationService.NotifyCommissionApprovedAsync(
                        item.Phone,
                        item.Name,
                        order.TrackingCode,
                        item.Commission,
                        item.CashBalance,
                        cancellationToken);
                }
            }
        }

        return new AffiliateCommissionSyncResult(approvedCount, rejectedCount);
    }
}
