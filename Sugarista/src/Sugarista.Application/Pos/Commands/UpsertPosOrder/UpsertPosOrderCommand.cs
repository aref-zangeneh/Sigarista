using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Accounting.Services;
using Sugarista.Application.Affiliates.Services;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.Customers.Services;
using Sugarista.Application.DeliveryMethods.Services;
using Sugarista.Application.Discounts.Engine;
using Sugarista.Application.Discounts.Security;
using Sugarista.Application.DrinkBuilder;
using Sugarista.Application.MenuItems.Bundles;
using Sugarista.Application.MenuItems.OptionGroups;
using Sugarista.Application.Orders;
using Sugarista.Application.Orders.Commands.CreateOrderCheckout;
using Sugarista.Application.Orders.Services;
using Sugarista.Domain.Constants;
using Sugarista.Domain.Entities;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Application.Pos.Commands.UpsertPosOrder;

public enum PosOrderAction
{
    Hold,
    PayCash,
    PayCard,
    PayCardToCard,
    PaySplit,
    InternalUse,
    Complimentary
}

public record PosPaymentAllocationRequest(
    string Method,
    decimal Amount,
    string? Reference = null);

public record PosOrderLineRequest(
    Guid MenuItemId,
    string? SizeKey,
    int Quantity,
    IReadOnlyCollection<Guid> ModifierIds,
    string? BaristaNote = null,
    string? PayerKey = null,
    string? PayerLabel = null,
    DrinkBuilderLineRequest? CustomDrink = null,
    IReadOnlyCollection<Guid>? OptionIds = null);

public record UpsertPosOrderCommand(
    Guid? OrderId,
    string? CustomerName,
    string? CustomerPhone,
    string? Note,
    string ServiceType,
    Guid? SalonTableId,
    IReadOnlyCollection<PosOrderLineRequest> Items,
    string Action,
    bool IsSplitBill = false,
    string? SplitBillMode = null,
    int? GuestCount = null,
    IReadOnlyCollection<CheckoutSplitShareRequest>? SplitShares = null,
    Guid? ServedByAdminUserId = null,
    Guid? DeliveryMethodId = null,
    bool PersonalCup = false,
    bool UseSubscription = false,
    string? OrderKind = null,
    string? ComplimentaryReason = null,
    bool ExcludeFromAccounting = false,
    string? ManualDiscountType = null,
    decimal? ManualDiscountValue = null,
    string? ManualDiscountReason = null,
    IReadOnlyCollection<PosPaymentAllocationRequest>? PaymentAllocations = null,
    string? PaymentReference = null,
    string? DiscountCode = null,
    int RedeemPoints = 0,
    string? ManagerNote = null) : IRequest<UpsertPosOrderResultDto>;

public class UpsertPosOrderCommandValidator : AbstractValidator<UpsertPosOrderCommand>
{
    public UpsertPosOrderCommandValidator()
    {
        RuleFor(c => c.Items).NotEmpty().WithMessage("At least one item is required.");
        RuleForEach(c => c.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.MenuItemId).NotEmpty();
            item.RuleFor(i => i.Quantity).GreaterThan(0);
            item.RuleFor(i => i.BaristaNote).MaximumLength(500);
        });
        RuleFor(c => c.ServiceType)
            .Must(value => Enum.TryParse<PosServiceType>(value, true, out _))
            .WithMessage("Service type must be DineIn or TakeAway.");
        RuleFor(c => c.Action)
            .Must(value => Enum.TryParse<PosOrderAction>(value, true, out _))
            .WithMessage("Action must be Hold, PayCash, PayCard, PayCardToCard, PaySplit, InternalUse, or Complimentary.");
        RuleFor(c => c.OrderKind)
            .Must(value => string.IsNullOrWhiteSpace(value) || Enum.TryParse<OrderKind>(value, true, out _))
            .WithMessage("OrderKind must be Standard, InternalUse, or Complimentary.");
        RuleFor(c => c.ComplimentaryReason).MaximumLength(300);
        RuleFor(c => c.Note).MaximumLength(500);
        RuleFor(c => c.ManagerNote).MaximumLength(1000);
        RuleFor(c => c.PaymentReference).MaximumLength(100);
        RuleFor(c => c.DiscountCode).MaximumLength(64);
        RuleFor(c => c.RedeemPoints).GreaterThanOrEqualTo(0);
        RuleFor(c => c.ManualDiscountType)
            .Must(value => string.IsNullOrWhiteSpace(value) || Enum.TryParse<ManualDiscountType>(value, true, out _))
            .WithMessage("ManualDiscountType must be Amount or Percent.");
        RuleFor(c => c.ManualDiscountValue)
            .GreaterThanOrEqualTo(0m)
            .When(c => c.ManualDiscountValue.HasValue)
            .WithMessage("Manual discount value cannot be negative.");
        RuleFor(c => c.ManualDiscountValue)
            .LessThanOrEqualTo(100m)
            .When(c => c.ManualDiscountValue.HasValue &&
                string.Equals(c.ManualDiscountType, nameof(Domain.Entities.ManualDiscountType.Percent), StringComparison.OrdinalIgnoreCase))
            .WithMessage("Manual discount percentage cannot exceed 100.");
        RuleFor(c => c.ManualDiscountReason).MaximumLength(300);

        When(c => string.Equals(c.Action, nameof(PosOrderAction.PaySplit), StringComparison.OrdinalIgnoreCase), () =>
        {
            RuleFor(c => c.PaymentAllocations)
                .NotEmpty()
                .WithMessage("در پرداخت ترکیبی باید حداقل یک ردیف پرداخت ثبت شود.");

            RuleForEach(c => c.PaymentAllocations!).ChildRules(leg =>
            {
                leg.RuleFor(l => l.Method)
                    .NotEmpty()
                    .Must(IsAllowedPosTenderMethod)
                    .WithMessage("روش پرداخت باید نقدی (Cash)، کارت‌خوان (Card) یا کارت به کارت (CardToCard) باشد.");
                leg.RuleFor(l => l.Amount)
                    .GreaterThan(0m)
                    .WithMessage("مبلغ هر ردیف پرداخت ترکیبی باید بزرگ‌تر از صفر باشد.");
                leg.RuleFor(l => l.Reference).MaximumLength(100);
            });
        });
    }

    private static bool IsAllowedPosTenderMethod(string? method) =>
        Enum.TryParse<PosPaymentMethod>(method, true, out var parsed) &&
        parsed is PosPaymentMethod.Cash or PosPaymentMethod.Card or PosPaymentMethod.CardToCard;
}

public class UpsertPosOrderCommandHandler : IRequestHandler<UpsertPosOrderCommand, UpsertPosOrderResultDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IAccountingService _accountingService;
    private readonly IPrintJobService _printJobService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILoyaltyService _loyaltyService;
    private readonly IDiscountEngine _discountEngine;
    private readonly IAffiliateTrackingService? _affiliateTrackingService;

    public UpsertPosOrderCommandHandler(
        IApplicationDbContext dbContext,
        IAccountingService accountingService,
        IPrintJobService printJobService,
        ICurrentUserService currentUserService,
        ILoyaltyService loyaltyService,
        IDiscountEngine discountEngine,
        IAffiliateTrackingService? affiliateTrackingService = null)
    {
        _dbContext = dbContext;
        _accountingService = accountingService;
        _printJobService = printJobService;
        _currentUserService = currentUserService;
        _loyaltyService = loyaltyService;
        _discountEngine = discountEngine;
        _affiliateTrackingService = affiliateTrackingService;
    }

    public async Task<UpsertPosOrderResultDto> Handle(UpsertPosOrderCommand request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<PosOrderAction>(request.Action, true, out var action))
        {
            throw new InvalidOperationException("Invalid POS action.");
        }

        if (!Enum.TryParse<PosServiceType>(request.ServiceType, true, out var serviceType))
        {
            throw new InvalidOperationException("Invalid service type.");
        }

        var canManageManagerNote = _currentUserService.HasPermission(AdminPermissions.Pos.ManagerNote);
        if (!canManageManagerNote && !string.IsNullOrWhiteSpace(request.ManagerNote))
        {
            throw new DomainException("شما مجوز ثبت یا ویرایش یادداشت مدیریت را ندارید.");
        }

        var splitBillMode = ResolveSplitBillMode(request);
        var isSplitBill = splitBillMode != SplitBillMode.Single;
        var resolvedLines = await ResolveLinesAsync(request.Items, cancellationToken);
        var itemsTotal = resolvedLines.Sum(line => line.resolved.UnitPrice * line.resolved.Quantity);
        var taxTotal = resolvedLines.Sum(line =>
            Math.Round(line.resolved.UnitPrice * line.resolved.Quantity * line.resolved.TaxPercent / 100m, 2));

        Domain.Entities.DeliveryMethod? deliveryMethod = null;
        if (request.DeliveryMethodId.HasValue)
        {
            deliveryMethod = await _dbContext.DeliveryMethods
                .FirstOrDefaultAsync(method => method.Id == request.DeliveryMethodId.Value, cancellationToken)
                ?? throw new KeyNotFoundException("Delivery method was not found.");

            if (!deliveryMethod.IsActive)
            {
                throw new InvalidOperationException($"Delivery method '{deliveryMethod.PersianName}' is inactive.");
            }
        }

        var deliveryFee = deliveryMethod?.DeliveryFee ?? 0m;
        var totalAmount = itemsTotal + deliveryFee + taxTotal;

        var customer = await CustomerDirectoryService.ResolveCustomerAsync(
            _dbContext,
            request.CustomerName,
            request.CustomerPhone,
            "pos",
            cancellationToken);

        var normalizedPhone = CustomerDirectoryService.NormalizePhone(request.CustomerPhone);
        var effectivePhone = customer?.PhoneNumber ?? (string.IsNullOrWhiteSpace(normalizedPhone) ? null : normalizedPhone);
        var effectiveName = !string.IsNullOrWhiteSpace(request.CustomerName)
            ? request.CustomerName.Trim()
            : customer is not null && CustomerDirectoryService.IsMeaningfulCustomerName(customer.PersianName, customer.PhoneNumber)
                ? customer.PersianName
                : null;

        var tableSessionId = await ResolveTableSessionIdAsync(request.SalonTableId, serviceType, cancellationToken);

        Order order;
        if (request.OrderId.HasValue)
        {
            order = await _dbContext.Orders
                .Include(o => o.Lines)
                .Include(o => o.Payments)
                .Include(o => o.SplitShares)
                .FirstOrDefaultAsync(o => o.Id == request.OrderId.Value, cancellationToken)
                ?? throw new KeyNotFoundException("Order was not found.");

            if (order.Channel != OrderChannel.Pos)
            {
                throw new InvalidOperationException("Only POS orders can be edited here.");
            }

            if (order.Status is OrderStatus.Cancelled or OrderStatus.Failed or OrderStatus.Paid)
            {
                throw new InvalidOperationException($"Order in status '{order.Status}' cannot be modified.");
            }

            await ReplaceLinesAsync(order, resolvedLines, cancellationToken);
            ReplaceSplitShares(order, request, splitBillMode, isSplitBill);
            order.RecalculateTotal();
        }
        else
        {
            var trackingCode = await OrderTrackingCodeGenerator.GenerateAsync(_dbContext, cancellationToken);
            order = Order.CreatePosDraft(
                trackingCode,
                effectiveName,
                effectivePhone,
                request.Note,
                totalAmount,
                serviceType,
                request.SalonTableId,
                tableSessionId,
                request.ServedByAdminUserId,
                splitBillMode,
                request.GuestCount);

            foreach (var (resolved, lineRequest, snapshot, optionSnapshot, bundleSnapshotJson) in resolvedLines)
            {
                order.AddLine(CreateLine(order.Id, resolved, lineRequest, snapshot, optionSnapshot, bundleSnapshotJson));
            }

            if (isSplitBill && request.SplitShares is { Count: > 0 })
            {
                AddSplitShares(order, request.SplitShares);
            }

            _dbContext.Orders.Add(order);
        }

        order.UpdateCustomerForPos(effectiveName, effectivePhone, request.Note);
        if (canManageManagerNote && request.ManagerNote is not null)
        {
            order.SetManagerNote(request.ManagerNote);
        }

        order.ConfigurePos(
            OrderChannel.Pos,
            serviceType,
            request.SalonTableId,
            tableSessionId,
            request.ServedByAdminUserId);

        order.ApplyDeliveryMethod(deliveryMethod?.Id, deliveryFee);
        order.AssignCustomer(customer?.Id);

        var siteSettings = await _dbContext.SiteSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        if (request.OrderId.HasValue)
        {
            var existingDiscounts = await _dbContext.OrderDiscounts
                .Where(discount => discount.OrderId == order.Id)
                .ToListAsync(cancellationToken);

            foreach (var existingDiscount in existingDiscounts)
            {
                order.Discounts.Remove(existingDiscount);
            }

            if (existingDiscounts.Count > 0)
            {
                _dbContext.OrderDiscounts.RemoveRange(existingDiscounts);
            }
        }

        var isNonRevenueAction = action is PosOrderAction.InternalUse or PosOrderAction.Complimentary;
        var isPaidRevenueAction = action is PosOrderAction.PayCash or PosOrderAction.PayCard or PosOrderAction.PayCardToCard or PosOrderAction.PaySplit;
        var resolvedOrderKind = action switch
        {
            PosOrderAction.InternalUse => Domain.Entities.OrderKind.InternalUse,
            PosOrderAction.Complimentary => Domain.Entities.OrderKind.Complimentary,
            _ => !string.IsNullOrWhiteSpace(request.OrderKind) &&
                 Enum.TryParse<Domain.Entities.OrderKind>(request.OrderKind, true, out var parsedKind)
                ? parsedKind
                : Domain.Entities.OrderKind.Standard
        };

        var adjustedCodeApplications = new List<DiscountApplication>();
        AffiliateCouponResolution? posAffiliateCoupon = null;
        var posCategoryByMenuItem = resolvedLines
            .GroupBy(line => line.resolved.MenuItemId)
            .ToDictionary(group => group.Key, group => group.First().resolved.CategoryId);

        if (!isNonRevenueAction)
        {
            var remainingDailyCap = await DiscountDailyCapService.GetRemainingAllowanceAsync(
                _dbContext,
                siteSettings,
                customer?.Id,
                siteSettings?.TimeZoneId,
                DateTimeOffset.UtcNow,
                request.OrderId,
                cancellationToken);

            var discountTracker = new OrderDiscountTracker(
                order,
                customer?.Id,
                remainingDailyCap,
                _dbContext.OrderDiscounts);

            var categoryByMenuItem = resolvedLines
                .GroupBy(line => line.resolved.MenuItemId)
                .ToDictionary(group => group.Key, group => (Guid?)group.First().resolved.CategoryId);

            // 1. Discount Engine (automatic POS campaigns + optional promo code)
            var requestedDiscountCode = string.IsNullOrWhiteSpace(request.DiscountCode)
                ? null
                : Discount.NormalizeCode(request.DiscountCode);

            var customerGroups = new List<DiscountCustomerGroup>();
            if (customer is not null)
            {
                var hasPriorPaidOrders = await _dbContext.Orders
                    .AnyAsync(o => o.CustomerId == customer.Id && o.Id != order.Id && o.Status == OrderStatus.Paid, cancellationToken);
                customerGroups.Add(hasPriorPaidOrders
                    ? DiscountCustomerGroup.Returning
                    : DiscountCustomerGroup.New);
            }

            var discountLines = resolvedLines
                .Select(entry => new DiscountLineInput(
                    entry.resolved.MenuItemId,
                    entry.resolved.CategoryId,
                    entry.resolved.PersianName,
                    entry.resolved.SizeKey,
                    entry.resolved.Quantity,
                    entry.resolved.UnitPrice,
                    entry.request.PayerKey))
                .ToList();

            var discountEvaluation = await _discountEngine.EvaluateAsync(
                new DiscountEvaluationContext(
                    discountLines,
                    DeliveryFee: deliveryFee,
                    Code: requestedDiscountCode,
                    CustomerId: customer?.Id,
                    CustomerGroups: customerGroups,
                    Now: DateTimeOffset.UtcNow,
                    Channel: "pos",
                    IsOnlinePayment: false,
                    TimeZoneId: siteSettings?.TimeZoneId ?? "Asia/Tehran"),
                cancellationToken);

            var posItemsGrossSubtotal = discountLines.Sum(line => line.UnitPrice * line.Quantity);
            if (requestedDiscountCode is not null && _affiliateTrackingService is not null)
            {
                posAffiliateCoupon = await _affiliateTrackingService.TryResolveCouponAsync(
                    requestedDiscountCode,
                    posItemsGrossSubtotal,
                    customer?.Id,
                    throwOnInvalidCoupon: true,
                    cancellationToken,
                    customerPhone: request.CustomerPhone);
            }

            if (requestedDiscountCode is not null &&
                posAffiliateCoupon is null &&
                discountEvaluation.Applications.Count == 0 &&
                discountEvaluation.Rejections.Count > 0)
            {
                throw new DomainException(
                    discountEvaluation.CodeRejection?.Message ?? "این کد تخفیف معتبر نیست.");
            }

            foreach (var application in discountEvaluation.Applications)
            {
                var scopes = application.LineShares
                    .GroupBy(share => categoryByMenuItem.GetValueOrDefault(share.MenuItemId))
                    .Select(group => new DiscountScopeAmount(group.Key, group.Sum(share => share.Amount)))
                    .ToList();

                var applied = discountTracker.Apply(
                    OrderDiscountKind.Code,
                    application.Name,
                    application.Amount,
                    application.DiscountId,
                    application.Code,
                    scopes);

                if (applied > 0)
                {
                    adjustedCodeApplications.Add(application with { Amount = applied });
                }
            }

            if (posAffiliateCoupon is not null && posAffiliateCoupon.DiscountAmount > 0m)
            {
                discountTracker.Apply(
                    OrderDiscountKind.Code,
                    $"کد معرف {posAffiliateCoupon.Code}",
                    posAffiliateCoupon.DiscountAmount,
                    discountId: null,
                    code: posAffiliateCoupon.Code,
                    scopes: null);
            }

            if (requestedDiscountCode is not null &&
                posAffiliateCoupon is null &&
                discountEvaluation.Applications.Count > 0 &&
                adjustedCodeApplications.Count == 0)
            {
                throw new DomainException("سقف تخفیف روزانهٔ این مشتری تکمیل شده است.");
            }

            // 2. Loyalty Points Redemption (only burned at final payment, not on Hold)
            if (isPaidRevenueAction &&
                request.RedeemPoints > 0 &&
                customer is not null &&
                siteSettings is { LoyaltyEnabled: true } &&
                siteSettings.LoyaltyRedeemTomanPerPoint > 0)
            {
                var points = Math.Min(request.RedeemPoints, (int)Math.Floor(customer.LoyaltyPoints));
                var maxPointsByOrder = (int)Math.Floor(order.TotalAmount / siteSettings.LoyaltyRedeemTomanPerPoint);
                points = Math.Min(points, maxPointsByOrder);
                if (discountTracker.RemainingCap is { } loyaltyCap)
                {
                    var maxPointsByCap = (int)Math.Floor(loyaltyCap / siteSettings.LoyaltyRedeemTomanPerPoint);
                    points = Math.Min(points, maxPointsByCap);
                }

                if (points > 0)
                {
                    var loyaltyDiscount = points * siteSettings.LoyaltyRedeemTomanPerPoint;
                    var appliedLoyalty = discountTracker.Apply(
                        OrderDiscountKind.LoyaltyPoints,
                        "امتیاز باشگاه مشتریان",
                        loyaltyDiscount);

                    if (appliedLoyalty > 0)
                    {
                        customer.DeductLoyaltyPoints(points);
                        _dbContext.LoyaltyTransactions.Add(new LoyaltyTransaction(
                            customer.Id,
                            LoyaltyTransactionType.Redeem,
                            -points,
                            customer.LoyaltyPoints,
                            $"مصرف امتیاز در سفارش صندوق {order.TrackingCode}",
                            order.Id,
                            null));
                    }
                }
            }

            // 3. Stamp Card Reward (only burned at final payment when no manual discount code was typed)
            if (isPaidRevenueAction &&
                string.IsNullOrWhiteSpace(request.DiscountCode) &&
                customer is not null &&
                siteSettings is { StampCardEnabled: true } &&
                siteSettings.StampCardRewardPercent > 0)
            {
                var stampPercent = await StampCardService.SpendActiveRewardAsync(_dbContext, customer, order.Id, cancellationToken);
                if (stampPercent > 0)
                {
                    var stampDiscount = Math.Round(itemsTotal * stampPercent / 100m, 0, MidpointRounding.AwayFromZero);
                    if (stampDiscount > 0)
                    {
                        discountTracker.Apply(OrderDiscountKind.StampCard, "پاداش کارت مهر", stampDiscount);
                    }
                }
            }

            // 4. Membership Tier Standing Discount
            if (customer is not null &&
                siteSettings is { MembershipTiersEnabled: true } &&
                customer.MembershipTierId is not null)
            {
                var tier = await _dbContext.MembershipTiers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(entry => entry.Id == customer.MembershipTierId, cancellationToken);
                if (tier is { IsEnabled: true } && tier.DiscountPercent > 0)
                {
                    var tierDiscount = Math.Round(itemsTotal * tier.DiscountPercent / 100m, 0, MidpointRounding.AwayFromZero);
                    if (tierDiscount > 0)
                    {
                        discountTracker.Apply(OrderDiscountKind.Tier, tier.Name, tierDiscount);
                    }
                }
            }

            // 5. Birthday Automatic Benefit
            if (customer is not null &&
                siteSettings is { BirthdayDiscountEnabled: true, BirthdayDiscountMode: BirthdayDiscountMode.Automatic } &&
                siteSettings.BirthdayDiscountPercent > 0 &&
                customer.DateOfBirth is not null &&
                BirthdayDiscountService.IsBirthdayActive(customer.DateOfBirth.Value, siteSettings.BirthdayDiscountWindowDays))
            {
                var birthdayDiscount = Math.Round(itemsTotal * siteSettings.BirthdayDiscountPercent / 100m, 0, MidpointRounding.AwayFromZero);
                if (birthdayDiscount > 0)
                {
                    discountTracker.Apply(OrderDiscountKind.Birthday, "هدیهٔ تولد", birthdayDiscount);
                }
            }

            // 6. Customer Group Standing Discount
            if (customer is not null)
            {
                var groupMembership = await CustomerGroupService.GetActiveMembershipAsync(
                    _dbContext, customer.Id, cancellationToken);
                if (groupMembership is { } membership && membership.Group.DiscountPercent > 0)
                {
                    var groupDiscount = Math.Round(itemsTotal * membership.Group.DiscountPercent / 100m, 0, MidpointRounding.AwayFromZero);
                    if (groupDiscount > 0)
                    {
                        discountTracker.Apply(
                            OrderDiscountKind.Group,
                            membership.Group.Name,
                            groupDiscount);
                    }
                }
            }

            // 7. Personal Cup Discount
            if (request.PersonalCup &&
                siteSettings is { PersonalCupDiscountEnabled: true } &&
                siteSettings.PersonalCupDiscountAmount > 0)
            {
                var cupCategories = SubscriptionService.ParseCategoryIds(siteSettings.PersonalCupCategoryIds);
                var eligibleLines = resolvedLines
                    .Where(line => cupCategories.Count == 0 || cupCategories.Contains(line.resolved.CategoryId))
                    .ToList();
                var eligibleAmount = eligibleLines.Sum(line => line.resolved.UnitPrice * line.resolved.Quantity);
                var cupDiscount = Math.Min(siteSettings.PersonalCupDiscountAmount, eligibleAmount);
                if (cupDiscount > 0)
                {
                    var cupScopes = eligibleLines
                        .GroupBy(line => line.resolved.CategoryId)
                        .Select(group => new DiscountScopeAmount(
                            group.Key,
                            group.Sum(line => line.resolved.UnitPrice * line.resolved.Quantity)))
                        .ToList();

                    discountTracker.Apply(
                        OrderDiscountKind.Cup,
                        "لیوان شخصی",
                        cupDiscount,
                        scopes: cupScopes);
                }
            }

            // 8. Monthly Subscription (burned only at final payment)
            if (request.UseSubscription &&
                customer is not null &&
                order.TotalAmount > 0 &&
                isPaidRevenueAction)
            {
                var subscription = await SubscriptionService.GetActiveAsync(
                    _dbContext, customer.Id, cancellationToken);
                if (subscription is not null)
                {
                    var plan = SubscriptionService.PlanRedemption(
                        subscription,
                        resolvedLines.Select(line => (
                            line.resolved.MenuItemId,
                            line.resolved.Quantity,
                            line.resolved.UnitPrice,
                            (IReadOnlyCollection<Guid>)new[] { line.resolved.CategoryId })));

                    var unitsToConsume = 0;
                    foreach (var unit in plan.Units)
                    {
                        var quantity = unit.Quantity;

                        if (discountTracker.RemainingCap is { } unitCap && unit.UnitPrice > 0)
                        {
                            quantity = Math.Min(
                                quantity,
                                (int)Math.Floor(unitCap / unit.UnitPrice));
                        }

                        if (quantity <= 0)
                        {
                            continue;
                        }

                        var unitAmount = unit.UnitPrice * quantity;
                        var applied = discountTracker.Apply(
                            OrderDiscountKind.Subscription,
                            "اشتراک ماهانه",
                            unitAmount,
                            scopes:
                            [
                                new DiscountScopeAmount(
                                    categoryByMenuItem.GetValueOrDefault(unit.MenuItemId),
                                    unitAmount)
                            ]);

                        if (applied <= 0)
                        {
                            continue;
                        }

                        unitsToConsume += quantity;
                    }

                    if (unitsToConsume > 0)
                    {
                        subscription.Consume(unitsToConsume, DateOnly.FromDateTime(DateTime.UtcNow));
                    }
                }
            }

            // 9. Authorized Manual POS Discount
            if (request.ManualDiscountValue is > 0m)
            {
                if (!_currentUserService.HasPermission(AdminPermissions.Pos.ApplyManualDiscount))
                {
                    throw new DomainException("شما مجوز اعمال تخفیف دلخواه روی سفارش در صندوق فروش را ندارید.");
                }

                var parsedManualType = ManualDiscountType.Amount;
                if (!string.IsNullOrWhiteSpace(request.ManualDiscountType) &&
                    !Enum.TryParse<ManualDiscountType>(request.ManualDiscountType, true, out parsedManualType))
                {
                    throw new DomainException("نوع تخفیف دلخواه نامعتبر است.");
                }

                var appliedBy = await ResolveAdminDisplayNameAsync(request.ServedByAdminUserId, cancellationToken);
                var appliedManual = order.ApplyManualDiscount(
                    parsedManualType,
                    request.ManualDiscountValue.Value,
                    request.ManualDiscountReason,
                    appliedBy);

                if (appliedManual > 0m)
                {
                    var label = BuildManualDiscountLabel(
                        parsedManualType,
                        request.ManualDiscountValue.Value,
                        request.ManualDiscountReason);
                    var manualRow = new OrderDiscount(
                        order.Id,
                        customer?.Id,
                        OrderDiscountKind.ManualPos,
                        label,
                        appliedManual);
                    order.Discounts.Add(manualRow);
                    _dbContext.OrderDiscounts.Add(manualRow);
                }
            }
        }

        if (!isNonRevenueAction)
        {
            ValidateSplitBill(request, splitBillMode, order.TotalAmount);
        }

        if (request.ExcludeFromAccounting)
        {
            order.ExcludeFromAccounting(
                string.IsNullOrWhiteSpace(request.ComplimentaryReason)
                    ? "خارج از حسابداری (صندوق)"
                    : request.ComplimentaryReason);
        }

        switch (action)
        {
            case PosOrderAction.Hold:
                order.ConfigureOrderKind(resolvedOrderKind, request.ComplimentaryReason);
                order.Hold();
                break;

            case PosOrderAction.PayCash:
                RecordSinglePosPayment(order, PosPaymentMethod.Cash, request.PaymentReference);
                await DeductPosOrderInventoryAsync(order, cancellationToken);
                break;

            case PosOrderAction.PayCard:
                RecordSinglePosPayment(order, PosPaymentMethod.Card, request.PaymentReference);
                await DeductPosOrderInventoryAsync(order, cancellationToken);
                break;

            case PosOrderAction.PayCardToCard:
                RecordSinglePosPayment(order, PosPaymentMethod.CardToCard, request.PaymentReference);
                await DeductPosOrderInventoryAsync(order, cancellationToken);
                break;

            case PosOrderAction.PaySplit:
                RecordSplitPosPayments(order, request.PaymentAllocations);
                await DeductPosOrderInventoryAsync(order, cancellationToken);
                break;

            case PosOrderAction.InternalUse:
            case PosOrderAction.Complimentary:
                order.CompleteWithoutCharge(resolvedOrderKind, request.ComplimentaryReason);
                await DeductPosOrderInventoryAsync(order, cancellationToken);
                break;
        }

        if (isPaidRevenueAction && adjustedCodeApplications.Count > 0)
        {
            await _discountEngine.RecordRedemptionsAsync(
                order.Id,
                customer?.Id,
                adjustedCodeApplications,
                cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (isPaidRevenueAction || isNonRevenueAction)
        {
            await _accountingService.PostOrderPaymentAsync(order, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            // Queue the thermal receipt copies (customer + barista) for the print agent.
            await _printJobService.EnqueueOrderReceiptsAsync(order, cancellationToken);
        }

        if (isPaidRevenueAction)
        {
            // Award loyalty points, stamp cards, badges, membership tiers, and wallet cashback.
            await _loyaltyService.AwardForPaidOrderAsync(order, cancellationToken);

            // In-store POS affiliate commission is immediately Approved since payment is settled.
            if (posAffiliateCoupon is not null && _affiliateTrackingService is not null)
            {
                await _affiliateTrackingService.RecordOrderCommissionAsync(
                    order,
                    AffiliateOrderSource.Pos,
                    posAffiliateCoupon.AffiliateId,
                    cookieTrackingToken: null,
                    posCategoryByMenuItem,
                    autoApprove: true,
                    cancellationToken);
            }
        }

        return new UpsertPosOrderResultDto(
            order.Id,
            order.TrackingCode,
            order.Status.ToString(),
            order.TotalAmount,
            order.PaymentMethod?.ToString(),
            order.OrderKind.ToString(),
            order.ComplimentaryReason,
            order.IsExcludedFromAccounting,
            order.DiscountTotal,
            order.ManualDiscountType?.ToString(),
            order.ManualDiscountValue,
            order.ManualDiscountAmount,
            order.ManualDiscountReason,
            order.ManualDiscountAppliedBy,
            order.CustomerId,
            order.CustomerName,
            string.Equals(order.CustomerPhone, "POS", StringComparison.OrdinalIgnoreCase) ? null : order.CustomerPhone,
            PosOrderMapper.MapPaymentLegs(order),
            canManageManagerNote ? order.ManagerNote : null);
    }

    private void RecordSinglePosPayment(Order order, PosPaymentMethod method, string? reference)
    {
        if (order.TotalAmount <= 0m)
        {
            order.PayAtPos(method);
            return;
        }

        var created = order.RecordPosPayments([(method, order.TotalAmount, reference)]);
        _dbContext.OrderPayments.AddRange(created);
    }

    private void RecordSplitPosPayments(Order order, IReadOnlyCollection<PosPaymentAllocationRequest>? allocations)
    {
        if (allocations is null || allocations.Count == 0)
        {
            throw new DomainException("در پرداخت ترکیبی باید حداقل یک ردیف پرداخت ثبت شود.");
        }

        var parsed = new List<(PosPaymentMethod Method, decimal Amount, string? Reference)>(allocations.Count);
        foreach (var item in allocations)
        {
            if (!Enum.TryParse<PosPaymentMethod>(item.Method, true, out var method) ||
                method is not (PosPaymentMethod.Cash or PosPaymentMethod.Card or PosPaymentMethod.CardToCard))
            {
                throw new DomainException("روش پرداخت نامعتبر است. فقط نقدی، کارت‌خوان یا کارت به کارت مجاز است.");
            }

            parsed.Add((method, item.Amount, item.Reference));
        }

        if (order.TotalAmount <= 0m)
        {
            order.PayAtPos(parsed[0].Method);
            return;
        }

        var created = order.RecordPosPayments(parsed);
        _dbContext.OrderPayments.AddRange(created);
    }

    private async Task DeductPosOrderInventoryAsync(Order order, CancellationToken cancellationToken)
    {
        await OrderInventoryService.DeductInventoryForOrderLinesAsync(
            _dbContext,
            order,
            order.Lines.ToList(),
            cancellationToken);
        await DeliveryMethodInventoryService.DeductForOrderAsync(_dbContext, order, cancellationToken);
    }

    private async Task<string?> ResolveAdminDisplayNameAsync(Guid? adminUserId, CancellationToken cancellationToken)
    {
        var resolvedId = adminUserId;
        if (!resolvedId.HasValue && Guid.TryParse(_currentUserService.UserId, out var claimId))
        {
            resolvedId = claimId;
        }

        if (!resolvedId.HasValue)
        {
            return null;
        }

        var adminUser = await _dbContext.AdminUsers
            .AsNoTracking()
            .Where(u => u.Id == resolvedId.Value)
            .Select(u => new { u.DisplayName, u.Username })
            .FirstOrDefaultAsync(cancellationToken);

        if (adminUser is null)
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(adminUser.DisplayName)
            ? adminUser.Username
            : adminUser.DisplayName;
    }

    private static string BuildManualDiscountLabel(ManualDiscountType type, decimal value, string? reason)
    {
        var baseLabel = type == ManualDiscountType.Percent
            ? $"تخفیف دلخواه صندوق ({value:0.##}٪)"
            : "تخفیف دلخواه صندوق";

        if (string.IsNullOrWhiteSpace(reason))
        {
            return baseLabel;
        }

        var combined = $"{baseLabel} — {reason.Trim()}";
        return combined.Length <= 180 ? combined : combined[..180];
    }

    private async Task ReplaceLinesAsync(
        Order order,
        IReadOnlyCollection<(OrderPricing.ResolvedLine resolved, PosOrderLineRequest request, DrinkBuilderSnapshot? snapshot, MenuItemOptionSnapshot? optionSnapshot, string? bundleSnapshotJson)> resolvedLines,
        CancellationToken cancellationToken)
    {
        foreach (var existingLine in order.Lines.ToList())
        {
            if (existingLine.InventoryDeductedQuantity > 0)
            {
                await OrderInventoryService.RestoreInventoryForOrderLinesAsync(
                    _dbContext,
                    order,
                    [existingLine],
                    cancellationToken);
            }

            _dbContext.OrderLines.Remove(existingLine);
        }

        order.Lines.Clear();

        foreach (var (resolved, lineRequest, snapshot, optionSnapshot, bundleSnapshotJson) in resolvedLines)
        {
            var line = CreateLine(order.Id, resolved, lineRequest, snapshot, optionSnapshot, bundleSnapshotJson);
            order.AddLine(line);
            _dbContext.OrderLines.Add(line);
        }
    }

    private static OrderLine CreateLine(
        Guid orderId,
        OrderPricing.ResolvedLine resolved,
        PosOrderLineRequest lineRequest,
        DrinkBuilderSnapshot? snapshot,
        MenuItemOptionSnapshot? optionSnapshot,
        string? bundleSnapshotJson) =>
        new(
            orderId,
            resolved.MenuItemId,
            resolved.PersianName,
            resolved.EnglishName,
            resolved.SizeKey,
            resolved.SizeLabel,
            resolved.Quantity,
            resolved.UnitPrice,
            resolved.ModifierLabels,
            lineRequest.PayerKey,
            lineRequest.PayerLabel,
            lineRequest.BaristaNote,
            resolved.TaxPercent,
            snapshot is null ? null : DrinkBuilderPricing.Serialize(snapshot),
            snapshot?.SummaryLabels,
            resolved.OptionLabels,
            optionSnapshot is { Components.Count: > 0 } ? MenuItemOptionPricing.Serialize(optionSnapshot) : null,
            bundleSnapshotJson);

    private async Task<IReadOnlyCollection<(OrderPricing.ResolvedLine resolved, PosOrderLineRequest request, DrinkBuilderSnapshot? snapshot, MenuItemOptionSnapshot? optionSnapshot, string? bundleSnapshotJson)>> ResolveLinesAsync(
        IReadOnlyCollection<PosOrderLineRequest> items,
        CancellationToken cancellationToken)
    {
        var menuItemIds = items.Select(item => item.MenuItemId).Distinct().ToList();
        var modifierIds = items.SelectMany(item => item.ModifierIds).Distinct().ToList();

        var menuItems = await _dbContext.MenuItems
            .Include(item => item.BundleItems)
            .ThenInclude(b => b.ComponentMenuItem)
            .Where(item => menuItemIds.Contains(item.Id) && item.IsActive)
            .ToListAsync(cancellationToken);

        if (menuItems.Count != menuItemIds.Count)
        {
            throw new KeyNotFoundException("One or more menu items were not found or are inactive.");
        }

        var modifiers = modifierIds.Count == 0
            ? []
            : await _dbContext.MenuModifiers
                .Where(modifier => modifierIds.Contains(modifier.Id) && modifier.IsActive)
                .ToListAsync(cancellationToken);

        var modifiersById = modifiers.ToDictionary(modifier => modifier.Id);

        var optionGroups = await _dbContext.MenuItemOptionGroups
            .AsNoTracking()
            .Include(group => group.Options)
                .ThenInclude(option => option.Ingredients)
                    .ThenInclude(ingredientRow => ingredientRow.Ingredient)
            .Where(group => menuItemIds.Contains(group.MenuItemId) && group.IsActive)
            .OrderBy(group => group.DisplayOrder)
            .ToListAsync(cancellationToken);

        var optionGroupsByItem = optionGroups
            .GroupBy(group => group.MenuItemId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<MenuItemOptionGroup>)group.ToList());

        var customDrinkItems = items
            .Where(item => item.CustomDrink is not null)
            .ToList();

        var builderProfilesById = new Dictionary<Guid, DrinkBuilderProfile>();
        if (customDrinkItems.Count > 0)
        {
            var builderEnabled = await _dbContext.SiteSettings
                .AsNoTracking()
                .Select(settings => (bool?)settings.CustomDrinkBuilderEnabled)
                .FirstOrDefaultAsync(cancellationToken) ?? true;

            if (!builderEnabled)
            {
                throw new DomainException("ساخت نوشیدنی سفارشی در حال حاضر غیرفعال است.");
            }

            var profileIds = customDrinkItems
                .Select(item => item.CustomDrink!.ProfileId)
                .Distinct()
                .ToList();

            var profiles = await _dbContext.DrinkBuilderProfiles
                .Include(profile => profile.Groups)
                .ThenInclude(group => group.Options)
                .Where(profile => profileIds.Contains(profile.Id) && profile.IsEnabled)
                .ToListAsync(cancellationToken);

            if (profiles.Count != profileIds.Count)
            {
                throw new DomainException("پروفایل نوشیدنی انتخاب‌شده در دسترس نیست.");
            }

            builderProfilesById = profiles.ToDictionary(profile => profile.Id);
        }

        return items
            .Select(item =>
            {
                var menuItem = menuItems.First(entry => entry.Id == item.MenuItemId);
                var resolved = OrderPricing.ResolveLine(
                    menuItem,
                    item.SizeKey,
                    item.Quantity,
                    modifiersById,
                    item.ModifierIds);

                MenuItemOptionSnapshot? optionSnapshot = null;
                if (item.OptionIds is { Count: > 0 } || optionGroupsByItem.ContainsKey(item.MenuItemId))
                {
                    optionSnapshot = MenuItemOptionPricing.Resolve(
                        optionGroupsByItem.GetValueOrDefault(item.MenuItemId),
                        item.OptionIds?
                            .Select(optionId => new MenuItemOptionSelectionRequest(optionId))
                            .ToList());

                    if (optionSnapshot.Components.Count > 0)
                    {
                        resolved = resolved with
                        {
                            UnitPrice = Math.Round(resolved.UnitPrice + optionSnapshot.OptionsTotal, 2, MidpointRounding.AwayFromZero),
                            OptionLabels = optionSnapshot.Labels,
                        };
                    }
                }

                DrinkBuilderSnapshot? snapshot = null;
                if (item.CustomDrink is not null)
                {
                    var profile = builderProfilesById[item.CustomDrink.ProfileId];

                    if (profile.MenuItemId != item.MenuItemId)
                    {
                        throw new DomainException("نوشیدنی سفارشی انتخاب‌شده با آیتم منو هم‌خوانی ندارد.");
                    }

                    snapshot = DrinkBuilderPricing.Resolve(profile, resolved.UnitPrice, item.CustomDrink.Selections);

                    resolved = resolved with
                    {
                        UnitPrice = snapshot.UnitPrice,
                        ModifierLabels = resolved.ModifierLabels
                            .Concat(snapshot.SummaryLabels)
                            .ToArray(),
                    };
                }

                string? bundleSnapshotJson = null;
                if (menuItem.IsBundle)
                {
                    if (menuItem.BundleItems.Count == 0 ||
                        menuItem.BundleItems.Any(b => b.ComponentMenuItem is null || !b.ComponentMenuItem.IsActive || !b.ComponentMenuItem.IsOrderable))
                    {
                        throw new DomainException($"برخی آیتم‌های زیرمجموعه باندل «{menuItem.PersianName}» در حال حاضر غیرفعال یا ناموجود هستند.");
                    }

                    var valuation = BundlePricingService.Evaluate(
                        menuItem,
                        menuItem.BundleItems.ToList(),
                        overrideBundleSellingPrice: resolved.UnitPrice);
                    bundleSnapshotJson = BundlePricingService.SerializeSnapshot(valuation);
                }

                return (resolved, item, snapshot, optionSnapshot, bundleSnapshotJson);
            })
            .ToArray();
    }

    private async Task<Guid?> ResolveTableSessionIdAsync(
        Guid? salonTableId,
        PosServiceType serviceType,
        CancellationToken cancellationToken)
    {
        if (serviceType != PosServiceType.DineIn || !salonTableId.HasValue)
        {
            return null;
        }

        var session = await _dbContext.TableSessions
            .AsNoTracking()
            .Where(entry => entry.TableId == salonTableId.Value && entry.EndedAt == null)
            .OrderByDescending(entry => entry.StartedAt)
            .Select(entry => new { entry.Id })
            .FirstOrDefaultAsync(cancellationToken);

        return session?.Id;
    }

    private static void ValidateSplitBill(
        UpsertPosOrderCommand request,
        SplitBillMode splitBillMode,
        decimal totalAmount)
    {
        if (splitBillMode == SplitBillMode.ByCustomAmount)
        {
            var shareTotal = request.SplitShares?.Sum(share => share.ShareAmount ?? 0) ?? 0;
            if (shareTotal != totalAmount)
            {
                throw new InvalidOperationException("Custom share amounts must sum to the order total.");
            }
        }

        if (splitBillMode == SplitBillMode.ByPercentage)
        {
            var percentageTotal = request.SplitShares?.Sum(share => share.SharePercentage ?? 0) ?? 0;
            if (percentageTotal != 100)
            {
                throw new InvalidOperationException("Share percentages must sum to 100.");
            }
        }
    }

    private static SplitBillMode ResolveSplitBillMode(UpsertPosOrderCommand command)
    {
        if (!string.IsNullOrWhiteSpace(command.SplitBillMode) &&
            Enum.TryParse<SplitBillMode>(command.SplitBillMode, true, out var parsedMode))
        {
            return parsedMode;
        }

        return command.IsSplitBill ? SplitBillMode.ByItem : SplitBillMode.Single;
    }

    private void ReplaceSplitShares(
        Order order,
        UpsertPosOrderCommand request,
        SplitBillMode splitBillMode,
        bool isSplitBill)
    {
        foreach (var share in order.SplitShares.ToList())
        {
            _dbContext.OrderSplitShares.Remove(share);
        }

        order.SplitShares.Clear();
        order.ConfigureSplitBill(splitBillMode, request.GuestCount);

        if (isSplitBill && request.SplitShares is { Count: > 0 })
        {
            AddSplitShares(order, request.SplitShares);
        }
    }

    private static void AddSplitShares(
        Order order,
        IReadOnlyCollection<CheckoutSplitShareRequest> splitShares)
    {
        var sortOrder = 0;
        foreach (var share in splitShares)
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
}
