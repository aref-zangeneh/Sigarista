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
    CustomerProfileMembershipDto? Membership,
    CustomerProfileWalletDto? Wallet,
    CustomerProfileBirthdayDto? Birthday,
    CustomerProfileGroupDto? Group,
    CustomerProfileSubscriptionDto? Subscription,
    CustomerProfilePersonalCupDto? PersonalCup,
    IReadOnlyCollection<CustomerProfileBadgeDto> Badges);

public record CustomerProfileWalletDto(
    decimal Balance,
    decimal CashbackPercent,
    int WindowDays,
    int ExpiryDays,
    int PaymentMaxPercent,
    IReadOnlyCollection<CustomerProfileTopUpPackageDto> TopUpPackages,
    IReadOnlyCollection<CustomerProfileWalletMovementDto> RecentMovements);

public record CustomerProfileTopUpPackageDto(
    decimal Amount,
    decimal BonusAmount);

public record CustomerProfileWalletMovementDto(
    WalletTransactionType Type,
    decimal Amount,
    decimal BalanceAfter,
    string Description,
    DateTimeOffset CreatedAt);

public record CustomerProfileBirthdayDto(
    bool Eligible,
    BirthdayDiscountMode Mode,
    decimal DiscountPercent,
    int WindowDays,
    string Message,
    string? PersonalCode,
    string? DateOfBirth,
    bool EditableByCustomer);

public record CustomerProfileGroupDto(
    string GroupName,
    string? Description,
    decimal DiscountPercent,
    DateTimeOffset? ExpiresAt);

public record CustomerProfileSubscriptionDto(
    string PackageName,
    int TotalUseCount,
    int RemainingUses,
    int AvailableToday,
    int DailyUseLimit,
    DateTimeOffset ExpiresAt,
    string RenewalPolicy,
    string CancelRefundPolicy,
    IReadOnlyCollection<Guid> AllowedCategoryIds);

public record CustomerProfilePersonalCupDto(
    bool Enabled,
    decimal DiscountAmount,
    string DeliveryNote,
    IReadOnlyCollection<Guid> CategoryIds);

public record CustomerProfileMembershipDto(
    string? TierName,
    string? TierDescription,
    decimal TierDiscountPercent,
    int WindowDays,
    decimal WindowSpent,
    string? NextTierName,
    decimal NextTierRemainingAmount);

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
        await MembershipTierService.EvaluateForCustomerAsync(_dbContext, customer, cancellationToken);

        var membershipWindowDays = settings.MembershipTiersEnabled ? settings.MembershipWindowDays : 0;
        CustomerProfileMembershipDto? membership = null;
        if (settings.MembershipTiersEnabled)
        {
            var tier = await _dbContext.MembershipTiers
                .AsNoTracking()
                .FirstOrDefaultAsync(tierEntry => tierEntry.Id == customer.MembershipTierId, cancellationToken);

            var windowSpent = paidOrders.Sum();
            if (membershipWindowDays > 0)
            {
                windowSpent = await MembershipTierService.GetWindowSpentAsync(
                    _dbContext, customer.Id, membershipWindowDays, cancellationToken);
            }

            var nextTier = await _dbContext.MembershipTiers
                .AsNoTracking()
                .Where(tier => tier.IsEnabled && tier.MinTotalPaidSpent > windowSpent)
                .OrderBy(tier => tier.MinTotalPaidSpent)
                .Select(tier => new { tier.Name, tier.MinTotalPaidSpent })
                .FirstOrDefaultAsync(cancellationToken);

            membership = new CustomerProfileMembershipDto(
                tier?.Name,
                tier?.Description,
                tier?.DiscountPercent ?? 0m,
                membershipWindowDays,
                windowSpent,
                nextTier?.Name,
                nextTier is null ? 0m : nextTier.MinTotalPaidSpent - windowSpent);
        }

        CustomerProfileWalletDto? wallet = null;
        if (settings.WalletEnabled)
        {
            var walletEntity = await _dbContext.CustomerWallets
                .FirstOrDefaultAsync(walletEntry => walletEntry.CustomerId == customer.Id, cancellationToken);

            if (walletEntity is not null)
            {
                // Expire due cashback before reporting/spending the balance,
                // with a fully logged expiry transaction of its own.
                await WalletService.ExpireDueCashbackAsync(_dbContext, walletEntity, cancellationToken);
            }

            // Tier-specific cashback percent: the customer's level may
            // override (or drop to zero) the global percent.
            var cashbackPercent = settings.WalletCashbackPercent;
            if (customer.MembershipTierId is not null)
            {
                var tierCashback = await _dbContext.MembershipTiers
                    .AsNoTracking()
                    .Where(tierEntry => tierEntry.Id == customer.MembershipTierId)
                    .Select(tierEntry => (decimal?)tierEntry.CashbackPercent)
                    .FirstOrDefaultAsync(cancellationToken);
                if (tierCashback.HasValue)
                {
                    cashbackPercent = tierCashback.Value;
                }
            }

            var topUpPackages = await _dbContext.WalletTopUpPackages
                .AsNoTracking()
                .Where(packageEntry => packageEntry.IsEnabled)
                .OrderBy(packageEntry => packageEntry.DisplayOrder)
                .Select(packageEntry => new CustomerProfileTopUpPackageDto(
                    packageEntry.Amount,
                    packageEntry.BonusAmount))
                .ToListAsync(cancellationToken);

            var recentMovements = await _dbContext.WalletTransactions
                .AsNoTracking()
                .Where(transaction => walletEntity != null && transaction.WalletId == walletEntity.Id)
                .OrderByDescending(transaction => transaction.CreatedAt)
                .Take(10)
                .Select(transaction => new CustomerProfileWalletMovementDto(
                    transaction.Type,
                    transaction.Amount,
                    transaction.BalanceAfter,
                    transaction.Description,
                    transaction.CreatedAt))
                .ToListAsync(cancellationToken);

            wallet = new CustomerProfileWalletDto(
                walletEntity?.Balance ?? 0m,
                cashbackPercent,
                settings.MembershipWindowDays,
                settings.WalletCashbackExpiryDays,
                settings.WalletPaymentMaxPercent,
                topUpPackages,
                recentMovements);
        }

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

        CustomerProfileBirthdayDto? birthday = null;
        if (settings.BirthdayDiscountEnabled)
        {
            var birthdayEligible = customer.DateOfBirth is not null && BirthdayDiscountService.IsBirthdayActive(
                customer.DateOfBirth.Value, settings.BirthdayDiscountWindowDays);

            string? personalCode = null;
            if (birthdayEligible && settings.BirthdayDiscountMode == BirthdayDiscountMode.Code)
            {
                var issuedCode = await BirthdayCodeService.EnsurePersonalCodeAsync(
                    _dbContext, customer, settings, cancellationToken);
                personalCode = issuedCode?.Code;
            }

            birthday = new CustomerProfileBirthdayDto(
                birthdayEligible,
                settings.BirthdayDiscountMode,
                settings.BirthdayDiscountPercent,
                settings.BirthdayDiscountWindowDays,
                settings.BirthdayGreetingMessage,
                personalCode,
                customer.DateOfBirth?.ToString("yyyy-MM-dd"),
                settings.BirthdayEditableByCustomer);
        }

        CustomerProfileGroupDto? group = null;
        var groupMembership = await CustomerGroupService.GetActiveMembershipAsync(
            _dbContext, customer.Id, cancellationToken);
        if (groupMembership is { } activeGroup)
        {
            group = new CustomerProfileGroupDto(
                activeGroup.Group.Name,
                activeGroup.Group.Description,
                activeGroup.Group.DiscountPercent,
                activeGroup.Membership.ExpiresAt);
        }

        CustomerProfileSubscriptionDto? subscriptionDto = null;
        var subscription = await SubscriptionService.GetActiveAsync(
            _dbContext, customer.Id, cancellationToken);
        if (subscription is not null)
        {
            subscriptionDto = new CustomerProfileSubscriptionDto(
                subscription.PackageName,
                subscription.TotalUseCount,
                subscription.RemainingUses,
                subscription.AvailableToday(DateOnly.FromDateTime(DateTime.UtcNow)),
                subscription.DailyUseLimit,
                subscription.ExpiresAt,
                settings.SubscriptionRenewalPolicy,
                settings.SubscriptionCancelRefundPolicy,
                SubscriptionService.ParseCategoryIds(subscription.AllowedCategoryIds));
        }

        CustomerProfilePersonalCupDto? personalCup = null;
        if (settings.PersonalCupDiscountEnabled && settings.PersonalCupDiscountAmount > 0)
        {
            personalCup = new CustomerProfilePersonalCupDto(
                true,
                settings.PersonalCupDiscountAmount,
                settings.PersonalCupDeliveryNote,
                SubscriptionService.ParseCategoryIds(settings.PersonalCupCategoryIds));
        }

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
            membership,
            wallet,
            birthday,
            group,
            subscriptionDto,
            personalCup,
            badgeDtos);
    }
}
