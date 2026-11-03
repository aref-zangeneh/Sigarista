using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Customers.Services;

/// <summary>
/// Stamp-card loyalty game: every completed card (a fixed number of paid orders)
/// grants the customer a percent discount, awarded automatically and redeemable
/// at checkout. Awarding is idempotent per completed card number.
/// </summary>
public static class StampCardService
{
    public static async Task AwardCardsAsync(
        IApplicationDbContext dbContext,
        Sugarista.Domain.Entities.SiteSettings settings,
        Order order,
        Customer customer,
        CancellationToken cancellationToken = default)
    {
        if (!settings.StampCardEnabled ||
            settings.StampCardOrdersPerCard <= 0 ||
            settings.StampCardRewardPercent <= 0)
        {
            return;
        }

        // Exclude the order being processed: it may or may not be persisted as
        // Paid yet, so it is added once explicitly below.
        var paidOrderCount = await dbContext.Orders
            .CountAsync(customerOrder => customerOrder.CustomerId == customer.Id &&
                                         customerOrder.Id != order.Id &&
                                         customerOrder.Status == OrderStatus.Paid, cancellationToken);

        if (order.CustomerId == customer.Id && order.Status == OrderStatus.Paid)
        {
            paidOrderCount++;
        }

        var completedCards = paidOrderCount / settings.StampCardOrdersPerCard;
        if (completedCards <= 0)
        {
            return;
        }

        var existingCardNumbers = await dbContext.StampCardRewards
            .Where(reward => reward.CustomerId == customer.Id)
            .Select(reward => reward.FullCardNumber)
            .ToListAsync(cancellationToken);

        var awarded = existingCardNumbers.ToHashSet();

        DateTimeOffset? expiresAt = settings.StampCardRewardValidityDays > 0
            ? DateTimeOffset.UtcNow.AddDays(settings.StampCardRewardValidityDays)
            : null;

        var changed = false;
        for (var cardNumber = 1; cardNumber <= completedCards; cardNumber++)
        {
            if (awarded.Contains(cardNumber))
            {
                continue;
            }

            var code = await GenerateUniqueCodeAsync(dbContext, cancellationToken);
            dbContext.StampCardRewards.Add(new StampCardReward(
                customer.Id,
                cardNumber,
                code,
                settings.StampCardRewardPercent,
                expiresAt));
            changed = true;
        }

        if (changed)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Redeems the customer's oldest still-valid stamp reward for this order.
    /// Returns the applied percent (0 when nothing is applied).
    /// </summary>
    public static async Task<decimal> SpendActiveRewardAsync(
        IApplicationDbContext dbContext,
        Customer customer,
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var reward = await dbContext.StampCardRewards
            .Where(entry => entry.CustomerId == customer.Id &&
                            entry.IsRedeemed == false &&
                            (entry.ExpiresAt == null || entry.ExpiresAt > now))
            .OrderBy(entry => entry.FullCardNumber)
            .FirstOrDefaultAsync(cancellationToken);

        if (reward is null)
        {
            return 0m;
        }

        reward.MarkRedeemed(now, orderId);
        return reward.RewardPercent;
    }

    private static async Task<string> GenerateUniqueCodeAsync(
        IApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var code = $"STAMP-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
            var exists = await dbContext.StampCardRewards
                .AnyAsync(reward => reward.Code == code, cancellationToken);
            if (!exists)
            {
                return code;
            }
        }

        return $"STAMP-{Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()}";
    }
}
