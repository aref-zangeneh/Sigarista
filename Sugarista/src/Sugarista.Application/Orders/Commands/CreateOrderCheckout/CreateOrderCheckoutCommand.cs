using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.Customers.Services;
using Sugarista.Application.Common.Options;
using Sugarista.Application.Discounts.Engine;
using Sugarista.Application.Orders;
using Sugarista.Domain.Entities;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Application.Orders.Commands.CreateOrderCheckout;

public record CheckoutLineRequest(
    Guid MenuItemId,
    string? SizeKey,
    int Quantity,
    IReadOnlyCollection<Guid> ModifierIds,
    string? PayerKey = null,
    string? PayerLabel = null);

public record CheckoutSplitShareRequest(
    string PayerKey,
    string PayerLabel,
    decimal? SharePercentage = null,
    decimal? ShareAmount = null);

public record CreateOrderCheckoutCommand(
    string CustomerName,
    string CustomerPhone,
    string? Note,
    IReadOnlyCollection<CheckoutLineRequest> Items,
    bool IsSplitBill = false,
    string? SplitBillMode = null,
    int? GuestCount = null,
    IReadOnlyCollection<CheckoutSplitShareRequest>? SplitShares = null,
    string? DiscountCode = null,
    int RedeemPoints = 0,
    DateTimeOffset? DeliveryAt = null) : IRequest<CreateOrderCheckoutResultDto>;

public record CreateOrderCheckoutResultDto(
    Guid OrderId,
    string RedirectUrl,
    string PaymentAuthority,
    decimal TotalAmount,
    bool IsSplitBill,
    string TrackingCode);

public class CreateOrderCheckoutCommandValidator : AbstractValidator<CreateOrderCheckoutCommand>
{
    public CreateOrderCheckoutCommandValidator()
    {
        RuleFor(c => c.CustomerName).NotEmpty().MaximumLength(200);
        RuleFor(c => c.CustomerPhone).NotEmpty().MaximumLength(30);
        RuleFor(c => c.Note).MaximumLength(500);
        RuleFor(c => c.DiscountCode).MaximumLength(64);
        RuleFor(c => c.Items).NotEmpty().WithMessage("At least one item is required.");
        RuleForEach(c => c.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.MenuItemId).NotEmpty();
            item.RuleFor(i => i.Quantity).GreaterThan(0);
        });

        RuleFor(c => c.SplitBillMode)
            .Must(mode => string.IsNullOrWhiteSpace(mode) || Enum.TryParse<SplitBillMode>(mode, true, out _))
            .WithMessage("Split bill mode must be Single, ByItem, ByPercentage, or ByCustomAmount.");

        When(c => ResolveSplitBillMode(c) == SplitBillMode.ByItem, () =>
        {
            RuleFor(c => c.GuestCount).GreaterThan(1).WithMessage("Split bill requires at least 2 guests.");
            RuleForEach(c => c.Items).ChildRules(item =>
            {
                item.RuleFor(i => i.PayerKey).NotEmpty().WithMessage("Each item must be assigned to a guest.");
            });
        });

        When(c => ResolveSplitBillMode(c) == SplitBillMode.ByPercentage, () =>
        {
            RuleFor(c => c.GuestCount).GreaterThan(1).WithMessage("Split bill requires at least 2 guests.");
            RuleFor(c => c.SplitShares).NotEmpty().WithMessage("Split shares are required for percentage split.");
            RuleForEach(c => c.SplitShares!).ChildRules(share =>
            {
                share.RuleFor(s => s.PayerKey).NotEmpty();
                share.RuleFor(s => s.SharePercentage).NotNull().InclusiveBetween(0, 100);
            });
            RuleFor(c => c.SplitShares!)
                .Must(shares => shares.Sum(share => share.SharePercentage ?? 0) == 100)
                .WithMessage("Share percentages must sum to 100.");
        });

        When(c => ResolveSplitBillMode(c) == SplitBillMode.ByCustomAmount, () =>
        {
            RuleFor(c => c.GuestCount).GreaterThan(1).WithMessage("Split bill requires at least 2 guests.");
            RuleFor(c => c.SplitShares).NotEmpty().WithMessage("Split shares are required for custom amount split.");
            RuleForEach(c => c.SplitShares!).ChildRules(share =>
            {
                share.RuleFor(s => s.PayerKey).NotEmpty();
                share.RuleFor(s => s.ShareAmount).NotNull().GreaterThanOrEqualTo(0);
            });
        });
    }

    private static SplitBillMode ResolveSplitBillMode(CreateOrderCheckoutCommand command)
    {
        if (!string.IsNullOrWhiteSpace(command.SplitBillMode) &&
            Enum.TryParse<SplitBillMode>(command.SplitBillMode, true, out var parsedMode))
        {
            return parsedMode;
        }

        return command.IsSplitBill ? SplitBillMode.ByItem : SplitBillMode.Single;
    }
}

public class CreateOrderCheckoutCommandHandler : IRequestHandler<CreateOrderCheckoutCommand, CreateOrderCheckoutResultDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IPaymentGateway _paymentGateway;
    private readonly PaymentOptions _paymentOptions;
    private readonly IDiscountEngine _discountEngine;

    public CreateOrderCheckoutCommandHandler(
        IApplicationDbContext dbContext,
        IPaymentGateway paymentGateway,
        IOptions<PaymentOptions> paymentOptions,
        IDiscountEngine discountEngine)
    {
        _dbContext = dbContext;
        _paymentGateway = paymentGateway;
        _paymentOptions = paymentOptions.Value;
        _discountEngine = discountEngine;
    }

    public async Task<CreateOrderCheckoutResultDto> Handle(CreateOrderCheckoutCommand request, CancellationToken cancellationToken)
    {
        PaymentAvailability.EnsureOnlinePaymentIsConfigured(_paymentOptions);

        var siteSettings = await _dbContext.SiteSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        if (siteSettings is { IsOnlineSalesEnabled: false })
        {
            throw new DomainException("فروش آنلاین در حال حاضر غیرفعال است؛ لطفاً بعداً دوباره تلاش کنید.");
        }

        var splitBillMode = ResolveSplitBillMode(request);
        var isSplitBill = splitBillMode != SplitBillMode.Single;

        var menuItemIds = request.Items.Select(i => i.MenuItemId).Distinct().ToList();
        var modifierIds = request.Items.SelectMany(i => i.ModifierIds).Distinct().ToList();

        var menuItems = await _dbContext.MenuItems
            .Where(m => menuItemIds.Contains(m.Id) && m.IsActive)
            .ToListAsync(cancellationToken);

        if (menuItems.Count != menuItemIds.Count)
        {
            throw new KeyNotFoundException("One or more menu items were not found or are inactive.");
        }

        var modifiers = modifierIds.Count == 0
            ? []
            : await _dbContext.MenuModifiers
                .Where(m => modifierIds.Contains(m.Id) && m.IsActive)
                .ToListAsync(cancellationToken);

        var modifiersById = modifiers.ToDictionary(m => m.Id);

        var resolvedLines = request.Items
            .Select(item =>
            {
                var menuItem = menuItems.First(m => m.Id == item.MenuItemId);
                var resolved = OrderPricing.ResolveLine(menuItem, item.SizeKey, item.Quantity, modifiersById, item.ModifierIds);
                return (resolved, item.PayerKey, item.PayerLabel);
            })
            .ToList();

        var itemsTotal = resolvedLines.Sum(line => line.resolved.UnitPrice * line.resolved.Quantity);
        var taxTotal = resolvedLines.Sum(line =>
            Math.Round(line.resolved.UnitPrice * line.resolved.Quantity * line.resolved.TaxPercent / 100m, 2));
        var totalAmount = itemsTotal + taxTotal;

        var customer = await CustomerDirectoryService.ResolveCustomerAsync(
            _dbContext,
            request.CustomerName,
            request.CustomerPhone,
            "online",
            cancellationToken);

        if (splitBillMode == SplitBillMode.ByCustomAmount)
        {
            var shareTotal = request.SplitShares!.Sum(share => share.ShareAmount ?? 0);
            if (shareTotal != totalAmount)
            {
                throw new InvalidOperationException("Custom share amounts must sum to the order total.");
            }
        }

        // Customer group resolution: first order ever (New) vs returning customer.
        // A first-order code should only reach customers without prior paid orders.
        var customerGroups = new List<DiscountCustomerGroup>();
        if (customer is not null)
        {
            var hasPriorOrders = await _dbContext.Orders
                .AnyAsync(order => order.CustomerId == customer.Id, cancellationToken);
            customerGroups.Add(hasPriorOrders
                ? DiscountCustomerGroup.Returning
                : DiscountCustomerGroup.New);
        }

        // Discount evaluation happens entirely on the backend: the engine is
        // the single source of truth for the final discounted order amount.
        var discountTotal = 0m;
        var discountLines = new List<DiscountLineInput>();
        var requestedDiscountCode = string.IsNullOrWhiteSpace(request.DiscountCode)
            ? null
            : Discount.NormalizeCode(request.DiscountCode);

        foreach (var (resolved, payerKey, payerLabel) in resolvedLines)
        {
            discountLines.Add(new DiscountLineInput(
                resolved.MenuItemId,
                resolved.CategoryId,
                resolved.PersianName,
                resolved.SizeKey,
                resolved.Quantity,
                resolved.UnitPrice,
                payerKey));
        }

        var discountEvaluation = await _discountEngine.EvaluateAsync(
            new DiscountEvaluationContext(
                discountLines,
                DeliveryFee: 0m,
                Code: requestedDiscountCode,
                CustomerId: customer?.Id,
                CustomerGroups: customerGroups,
                Now: DateTimeOffset.UtcNow,
                Channel: "online",
                IsOnlinePayment: true,
                TimeZoneId: siteSettings?.TimeZoneId ?? "Asia/Tehran"),
            cancellationToken);

        discountTotal = discountEvaluation.Applications.Sum(application => application.Amount);

        // A typed code that produced no application means the engine rejected it
        // (unknown / inactive / expired / out of schedule / below minimum cart /
        // wrong customer group / no eligible items / not stackable). Surface the
        // domain message to the customer instead of silently creating an order
        // without any discount.
        if (requestedDiscountCode is not null &&
            discountEvaluation.Applications.Count == 0 &&
            discountEvaluation.Rejections.Count > 0)
        {
            throw new DomainException(
                discountEvaluation.CodeRejection?.Message ?? "این کد تخفیف معتبر نیست.");
        }

        if (discountTotal > 0)
        {
            await ApplyReferralRewardAsync(customer, request.DiscountCode, cancellationToken);
        }

        var trackingCode = await OrderTrackingCodeGenerator.GenerateAsync(_dbContext, cancellationToken);
        var paymentAuthority = isSplitBill ? $"SPLIT-{Guid.NewGuid():N}" : Guid.NewGuid().ToString("N");

        var order = new Order(
            trackingCode,
            request.CustomerName,
            request.CustomerPhone,
            request.Note,
            totalAmount,
            paymentAuthority,
            splitBillMode,
            request.GuestCount);

        foreach (var (resolved, payerKey, payerLabel) in resolvedLines)
        {
            order.AddLine(new OrderLine(
                order.Id,
                resolved.MenuItemId,
                resolved.PersianName,
                resolved.EnglishName,
                resolved.SizeKey,
                resolved.SizeLabel,
                resolved.Quantity,
                resolved.UnitPrice,
                resolved.ModifierLabels,
                payerKey,
                payerLabel,
                null,
                resolved.TaxPercent));
        }

        order.RecalculateTotal();

        if (discountTotal > 0)
        {
            order.ApplyDiscount(discountTotal);
        }

        // Loyalty redemption: convert points to a discount amount.
        if (request.RedeemPoints > 0 && customer is not null && siteSettings is { LoyaltyEnabled: true } &&
            siteSettings.LoyaltyRedeemTomanPerPoint > 0)
        {
            var points = Math.Min(request.RedeemPoints, customer.LoyaltyPoints);
            if (points > 0)
            {
                var loyaltyDiscount = points * siteSettings.LoyaltyRedeemTomanPerPoint;
                customer.DeductLoyaltyPoints(points);
                _dbContext.LoyaltyTransactions.Add(new LoyaltyTransaction(
                    customer.Id,
                    LoyaltyTransactionType.Redeem,
                    -points,
                    customer.LoyaltyPoints,
                    $"مصرف امتیاز در سفارش {order.TrackingCode}",
                    order.Id,
                    null));
                order.ApplyDiscount(loyaltyDiscount);
                discountTotal += loyaltyDiscount;
            }
        }

        // Stamp card: automatically spend the customer's oldest valid completed-card
        // reward when no manual discount code was entered.
        if (string.IsNullOrWhiteSpace(request.DiscountCode) && customer is not null &&
            siteSettings is { StampCardEnabled: true } && siteSettings.StampCardRewardPercent > 0)
        {
            var stampPercent = await StampCardService.SpendActiveRewardAsync(_dbContext, customer, order.Id, cancellationToken);
            if (stampPercent > 0)
            {
                var stampDiscount = Math.Round(itemsTotal * stampPercent / 100m, 2);
                if (stampDiscount > 0)
                {
                    order.ApplyDiscount(stampDiscount);
                    discountTotal += stampDiscount;
                }
            }
        }

        // Membership level: the standing permanent discount of the customer's
        // current level applies automatically, on top of manual codes.
        if (customer is not null &&
            siteSettings is { MembershipTiersEnabled: true } &&
            customer.MembershipTierId is not null)
        {
            var tier = await _dbContext.MembershipTiers
                .AsNoTracking()
                .FirstOrDefaultAsync(entry => entry.Id == customer.MembershipTierId, cancellationToken);
            if (tier is { IsEnabled: true } && tier.DiscountPercent > 0)
            {
                var tierDiscount = Math.Round(itemsTotal * tier.DiscountPercent / 100m, 2);
                if (tierDiscount > 0)
                {
                    order.ApplyDiscount(tierDiscount);
                    discountTotal += tierDiscount;
                }
            }
        }

        order.AssignCustomer(customer?.Id);

        order.ApplyDeliveryTime(request.DeliveryAt);

        if (isSplitBill && request.SplitShares is { Count: > 0 })
        {
            var sortOrder = 0;
            foreach (var share in request.SplitShares)
            {
                order.AddSplitShare(new OrderSplitShare(
                    order.Id,
                    share.PayerKey,
                    share.PayerLabel,
                    share.SharePercentage,
                    share.ShareAmount,
                    sortOrder++));
            }
        }

        _dbContext.Orders.Add(order);

        if (discountTotal > 0)
        {
            await _discountEngine.RecordRedemptionsAsync(
                order.Id,
                customer?.Id,
                discountEvaluation.Applications,
                cancellationToken);
        }

        if (isSplitBill)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);

            var splitUrl = $"{_paymentOptions.PublicBaseUrl.TrimEnd('/')}/?split={order.Id}";
            return new CreateOrderCheckoutResultDto(
                order.Id,
                splitUrl,
                paymentAuthority,
                order.TotalAmount,
                true,
                order.TrackingCode);
        }

        var callbackUrl = $"{_paymentOptions.PublicBaseUrl.TrimEnd('/')}/?pay={order.Id}";
        var payment = await _paymentGateway.RequestPaymentAsync(
            order.Id,
            order.TotalAmount,
            callbackUrl,
            $"Sugarista order {order.TrackingCode}",
            cancellationToken);

        order.UpdatePaymentAuthority(payment.Authority);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new CreateOrderCheckoutResultDto(
            order.Id,
            payment.RedirectUrl,
            payment.Authority,
            order.TotalAmount,
            false,
            order.TrackingCode);
    }

    /// <summary>
    /// Referral reward: when a discount was applied through a personal referral
    /// code, the invitee is ordering for the first time — the referring customer
    /// earns the referral reward (win-back / discount unlock).
    /// </summary>
    private async Task ApplyReferralRewardAsync(Customer? customer, string? discountCode, CancellationToken cancellationToken)
    {
        if (customer is null || string.IsNullOrWhiteSpace(discountCode) || discountCode.Length > 64)
        {
            return;
        }

        var normalizedCode = Discount.NormalizeCode(discountCode);
        var referrer = await _dbContext.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(referrer => referrer.ReferralCode == normalizedCode, cancellationToken);

        if (referrer is null || referrer.Id == customer.Id)
        {
            return;
        }

        var inviteeCompletedBefore = await _dbContext.Orders
            .AnyAsync(order => order.CustomerId == customer.Id && order.Status == OrderStatus.Paid && order.PaidAt != null, cancellationToken);

        if (inviteeCompletedBefore)
        {
            // Only the first successful order of an invitee unlocks the reward.
            return;
        }

        var referrerEntity = await _dbContext.Customers
            .FirstOrDefaultAsync(referrer => referrer.Id == referrer.Id, cancellationToken);

        if (referrerEntity is not null)
        {
            referrerEntity.MarkReferralRewardEligible();
        }
    }

    private static SplitBillMode ResolveSplitBillMode(CreateOrderCheckoutCommand command)
    {
        if (!string.IsNullOrWhiteSpace(command.SplitBillMode) &&
            Enum.TryParse<SplitBillMode>(command.SplitBillMode, true, out var parsedMode))
        {
            return parsedMode;
        }

        return command.IsSplitBill ? SplitBillMode.ByItem : SplitBillMode.Single;
    }
}
