using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.Customers.Services;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Customers.Queries.GetCustomerProfile;

/// <summary>
/// Public profile used by the storefront "stamp card" feature: the customer
/// identifies themselves by phone number (no login) and gets back their
/// stamps, completed-card reward, loyalty points and earned badges.
/// Profiles missing from the directory are created on the spot, so the very
/// first visit already enrolls the customer.
/// </summary>
public record GetCustomerProfileQuery(string PhoneNumber) : IRequest<CustomerProfileDto?>;

public record CustomerProfileDto(
    Guid CustomerId,
    string PersianName,
    decimal LoyaltyPoints,
    int PaidOrders,
    decimal TotalSpent,
    bool StampCardEnabled,
    int OrdersPerCard,
    int CurrentCardStamps,
    int CompletedCards,
    CustomerProfileRewardDto? ActiveReward,
    IReadOnlyCollection<CustomerProfileBadgeDto> Badges);

public record CustomerProfileRewardDto(
    int CardNumber,
    string Code,
    decimal Percent,
    DateTimeOffset? ExpiresAt);

public record CustomerProfileBadgeDto(
    Guid Id,
    string Title,
    string? Description,
    string? ImageUrl,
    DateTimeOffset? EarnedAt,
    bool Earned);

public class GetCustomerProfileQueryHandler : IRequestHandler<GetCustomerProfileQuery, CustomerProfileDto?>
{
    private readonly IApplicationDbContext _dbContext;

    public GetCustomerProfileQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<CustomerProfileDto?> Handle(GetCustomerProfileQuery request, CancellationToken cancellationToken)
    {
        var phone = CustomerDirectoryService.NormalizePhone(request.PhoneNumber);
        if (phone.Length < 4)
        {
            return null;
        }

        var customer = await CustomerDirectoryService.ResolveCustomerAsync(
            _dbContext,
            customerName: null,
            phone,
            "online",
            cancellationToken);

        if (customer is null)
        {
            return null;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var settings = await _dbContext.SiteSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken) ??
                       new Sugarista.Domain.Entities.SiteSettings();

        var paidOrders = await _dbContext.Orders
            .Where(orderEntry => orderEntry.CustomerId == customer.Id && orderEntry.Status == OrderStatus.Paid)
            .Select(orderEntry => orderEntry.TotalAmount)
            .ToListAsync(cancellationToken);

        var paidOrderCount = paidOrders.Count;
        var totalSpent = paidOrders.Sum();

        // Catch-up sweep: customers who already satisfy a newly created badge
        // earn it the next time their profile is viewed.
        await BadgeService.EvaluateForCustomerAsync(_dbContext, customer, cancellationToken);

        var completedCards = settings.StampCardOrdersPerCard > 0
            ? paidOrderCount / settings.StampCardOrdersPerCard
            : 0;

        var currentCardStamps = settings.StampCardOrdersPerCard > 0
            ? paidOrderCount % settings.StampCardOrdersPerCard
            : 0;

        var now = DateTimeOffset.UtcNow;
        var activeReward = await _dbContext.StampCardRewards
            .Where(reward => reward.CustomerId == customer.Id &&
                             reward.IsRedeemed == false &&
                             (reward.ExpiresAt == null || reward.ExpiresAt > now))
            .OrderBy(reward => reward.FullCardNumber)
            .Select(reward => new CustomerProfileRewardDto(
                reward.FullCardNumber,
                reward.Code,
                reward.RewardPercent,
                reward.ExpiresAt))
            .FirstOrDefaultAsync(cancellationToken);

        var badges = await _dbContext.Badges
            .AsNoTracking()
            .Where(entry => entry.IsEnabled)
            .OrderBy(entry => entry.DisplayOrder)
            .Select(entry => new
            {
                entry.Id,
                entry.Name,
                entry.Description,
                entry.ImageUrl,
                entry.MinPaidOrders,
                entry.MinTotalSpent,
                EarnedAt = entry.Awards
                    .Where(award => award.CustomerId == customer.Id)
                    .Select(award => (DateTimeOffset?)award.CreatedAt)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        var badgeDtos = badges.Select(entry =>
        {
            // Fallback for customers who already qualify but haven't been
            // swept yet: live-evaluate so newly defined badges surface immediately.
            var earned = entry.EarnedAt.HasValue ||
                         (entry.MinPaidOrders.HasValue && paidOrderCount >= entry.MinPaidOrders.Value) ||
                         (entry.MinTotalSpent.HasValue && totalSpent >= entry.MinTotalSpent.Value);
            return new CustomerProfileBadgeDto(
                entry.Id,
                entry.Name,
                entry.Description,
                entry.ImageUrl,
                entry.EarnedAt,
                earned);
        }).ToList();

        return new CustomerProfileDto(
            customer.Id,
            customer.PersianName,
            customer.LoyaltyPoints,
            paidOrderCount,
            totalSpent,
            settings.StampCardEnabled,
            settings.StampCardOrdersPerCard,
            currentCardStamps,
            completedCards,
            activeReward,
            badgeDtos);
    }
}
