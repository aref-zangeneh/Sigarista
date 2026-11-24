using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.Customers.Services;
using Sugarista.Application.Discounts.Security;
using Sugarista.Application.Sms;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Pos.Queries.GetPosCustomerByPhone;

public record GetPosCustomerByPhoneQuery(string? Phone) : IRequest<PosCustomerLookupDto>;

public class GetPosCustomerByPhoneQueryHandler : IRequestHandler<GetPosCustomerByPhoneQuery, PosCustomerLookupDto>
{
    private readonly IApplicationDbContext _dbContext;

    public GetPosCustomerByPhoneQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PosCustomerLookupDto> Handle(GetPosCustomerByPhoneQuery request, CancellationToken cancellationToken)
    {
        var normalizedPhone = CustomerDirectoryService.NormalizePhone(request.Phone);
        var settings = await _dbContext.SiteSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        var loyaltyEnabled = settings?.LoyaltyEnabled ?? false;
        var loyaltyRedeemRate = settings?.LoyaltyRedeemTomanPerPoint ?? 0m;
        var walletEnabled = settings?.WalletEnabled ?? false;
        var stampOrdersPerCard = settings is { StampCardEnabled: true } ? settings.StampCardOrdersPerCard : 0;

        if (string.IsNullOrWhiteSpace(normalizedPhone) || normalizedPhone.Length < 4)
        {
            return EmptyResult(string.Empty, loyaltyEnabled, loyaltyRedeemRate, walletEnabled, stampOrdersPerCard);
        }

        var smsMobile = SmsTextUtilities.NormalizeMobile(normalizedPhone);
        var altPlusMobile = smsMobile is not null ? $"+{smsMobile}" : null;

        var customer = await _dbContext.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(
                c => c.PhoneNumber == normalizedPhone ||
                     (smsMobile != null && (c.PhoneNumber == smsMobile || c.PhoneNumber == altPlusMobile)),
                cancellationToken);

        var smsContact = smsMobile is null
            ? null
            : await _dbContext.SmsContacts
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Mobile == smsMobile, cancellationToken);

        if (customer is null)
        {
            var contactName = smsContact is null
                ? null
                : string.Join(' ', new[] { smsContact.FirstName, smsContact.LastName }.Where(s => !string.IsNullOrWhiteSpace(s))).Trim();

            return new PosCustomerLookupDto(
                Exists: false,
                CustomerId: null,
                NormalizedPhone: normalizedPhone,
                PersianName: string.IsNullOrWhiteSpace(contactName) ? null : contactName,
                InSmsContacts: smsContact is not null,
                LoyaltyPoints: 0m,
                LoyaltyEnabled: loyaltyEnabled,
                LoyaltyRedeemTomanPerPoint: loyaltyRedeemRate,
                WalletBalance: 0m,
                WalletEnabled: walletEnabled,
                MembershipTierName: null,
                MembershipTierDiscountPercent: 0m,
                ActiveGroupName: null,
                ActiveGroupDiscountPercent: 0m,
                IsBirthdayDiscountActive: false,
                BirthdayDiscountPercent: 0m,
                HasActiveStampReward: false,
                StampRewardPercent: 0m,
                PaidOrdersCount: 0,
                StampOrdersPerCard: stampOrdersPerCard,
                ActiveSubscriptionName: null,
                SubscriptionRemainingUnits: 0,
                RemainingDailyDiscountAllowance: settings is { DiscountAbuseProtectionEnabled: true, DiscountDailyCapPerCustomer: > 0 }
                    ? settings.DiscountDailyCapPerCustomer
                    : null);
        }

        var displayName = CustomerDirectoryService.IsMeaningfulCustomerName(customer.PersianName, customer.PhoneNumber)
            ? customer.PersianName
            : smsContact is not null
                ? string.Join(' ', new[] { smsContact.FirstName, smsContact.LastName }.Where(s => !string.IsNullOrWhiteSpace(s))).Trim()
                : null;

        var walletBalance = walletEnabled
            ? await _dbContext.CustomerWallets
                .AsNoTracking()
                .Where(w => w.CustomerId == customer.Id)
                .Select(w => (decimal?)w.Balance)
                .FirstOrDefaultAsync(cancellationToken) ?? 0m
            : 0m;

        string? tierName = null;
        decimal tierDiscountPercent = 0m;
        if (settings is { MembershipTiersEnabled: true } && customer.MembershipTierId.HasValue)
        {
            var tier = await _dbContext.MembershipTiers
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == customer.MembershipTierId.Value && t.IsEnabled, cancellationToken);
            if (tier is not null)
            {
                tierName = tier.Name;
                tierDiscountPercent = tier.DiscountPercent;
            }
        }

        var groupMembership = await CustomerGroupService.GetActiveMembershipAsync(_dbContext, customer.Id, cancellationToken);
        var activeGroupName = groupMembership?.Group.Name;
        var activeGroupDiscountPercent = groupMembership?.Group.DiscountPercent ?? 0m;

        var isBirthdayActive = settings is { BirthdayDiscountEnabled: true, BirthdayDiscountMode: BirthdayDiscountMode.Automatic } &&
                               settings.BirthdayDiscountPercent > 0 &&
                               customer.DateOfBirth.HasValue &&
                               BirthdayDiscountService.IsBirthdayActive(customer.DateOfBirth.Value, settings.BirthdayDiscountWindowDays);
        var birthdayPercent = isBirthdayActive ? settings!.BirthdayDiscountPercent : 0m;

        var now = DateTimeOffset.UtcNow;
        var activeStampReward = settings is { StampCardEnabled: true } && settings.StampCardRewardPercent > 0
            ? await _dbContext.StampCardRewards
                .AsNoTracking()
                .Where(r => r.CustomerId == customer.Id && !r.IsRedeemed && (r.ExpiresAt == null || r.ExpiresAt > now))
                .OrderBy(r => r.FullCardNumber)
                .FirstOrDefaultAsync(cancellationToken)
            : null;

        var paidOrdersCount = await _dbContext.Orders
            .AsNoTracking()
            .CountAsync(o => o.CustomerId == customer.Id && o.Status == OrderStatus.Paid, cancellationToken);

        var subscription = await SubscriptionService.GetActiveAsync(_dbContext, customer.Id, cancellationToken);
        var subscriptionName = subscription?.PackageName ?? subscription?.Package?.Name;
        var subscriptionRemaining = subscription?.RemainingUses ?? 0;

        var remainingAllowance = await DiscountDailyCapService.GetRemainingAllowanceAsync(
            _dbContext,
            settings,
            customer.Id,
            settings?.TimeZoneId,
            now,
            cancellationToken: cancellationToken);

        return new PosCustomerLookupDto(
            Exists: true,
            CustomerId: customer.Id,
            NormalizedPhone: customer.PhoneNumber,
            PersianName: string.IsNullOrWhiteSpace(displayName) ? null : displayName,
            InSmsContacts: smsContact is not null,
            LoyaltyPoints: customer.LoyaltyPoints,
            LoyaltyEnabled: loyaltyEnabled,
            LoyaltyRedeemTomanPerPoint: loyaltyRedeemRate,
            WalletBalance: walletBalance,
            WalletEnabled: walletEnabled,
            MembershipTierName: tierName,
            MembershipTierDiscountPercent: tierDiscountPercent,
            ActiveGroupName: activeGroupName,
            ActiveGroupDiscountPercent: activeGroupDiscountPercent,
            IsBirthdayDiscountActive: isBirthdayActive,
            BirthdayDiscountPercent: birthdayPercent,
            HasActiveStampReward: activeStampReward is not null,
            StampRewardPercent: activeStampReward?.RewardPercent ?? 0m,
            PaidOrdersCount: paidOrdersCount,
            StampOrdersPerCard: stampOrdersPerCard,
            ActiveSubscriptionName: subscriptionName,
            SubscriptionRemainingUnits: subscriptionRemaining,
            RemainingDailyDiscountAllowance: remainingAllowance);
    }

    private static PosCustomerLookupDto EmptyResult(
        string normalizedPhone,
        bool loyaltyEnabled,
        decimal loyaltyRedeemRate,
        bool walletEnabled,
        int stampOrdersPerCard) =>
        new(
            Exists: false,
            CustomerId: null,
            NormalizedPhone: normalizedPhone,
            PersianName: null,
            InSmsContacts: false,
            LoyaltyPoints: 0m,
            LoyaltyEnabled: loyaltyEnabled,
            LoyaltyRedeemTomanPerPoint: loyaltyRedeemRate,
            WalletBalance: 0m,
            WalletEnabled: walletEnabled,
            MembershipTierName: null,
            MembershipTierDiscountPercent: 0m,
            ActiveGroupName: null,
            ActiveGroupDiscountPercent: 0m,
            IsBirthdayDiscountActive: false,
            BirthdayDiscountPercent: 0m,
            HasActiveStampReward: false,
            StampRewardPercent: 0m,
            PaidOrdersCount: 0,
            StampOrdersPerCard: stampOrdersPerCard,
            ActiveSubscriptionName: null,
            SubscriptionRemainingUnits: 0,
            RemainingDailyDiscountAllowance: null);
}
