using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.Customers.Services;
using Sugarista.Application.Common.Options;
using Sugarista.Application.Discounts.Engine;
using Sugarista.Application.Inventory.Services;
using Sugarista.Application.MenuItems.OptionGroups;
using Sugarista.Application.Discounts.Security;
using Sugarista.Application.DrinkBuilder;
using Sugarista.Application.Orders;
using Sugarista.Application.Orders.Commands.ConfirmMockPayment;
using Sugarista.Application.Pickup;
using Sugarista.Domain.Entities;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Application.Orders.Commands.CreateOrderCheckout;

public record CheckoutLineRequest(
    Guid MenuItemId,
    string? SizeKey,
    int Quantity,
    IReadOnlyCollection<Guid> ModifierIds,
    string? PayerKey = null,
    string? PayerLabel = null,
    DrinkBuilderLineRequest? CustomDrink = null,
    string? ClientLineKey = null,
    IReadOnlyCollection<Guid>? OptionIds = null);

public record CheckoutPriceSnapshot(
    string? ClientLineKey,
    Guid MenuItemId,
    string? SizeKey,
    decimal UnitPrice);

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
    bool UseWallet = false,
    bool PersonalCup = false,
    bool UseSubscription = false,
    DateTimeOffset? DeliveryAt = null,
    string? IpAddress = null,
    decimal? ExpectedTotalAmount = null,
    IReadOnlyCollection<CheckoutPriceSnapshot>? ExpectedLinePrices = null,
    bool AcceptPriceChanges = false,
    string? IdempotencyKey = null) : IRequest<CreateOrderCheckoutResultDto>;

public record CreateOrderCheckoutResultDto(
    Guid OrderId,
    string RedirectUrl,
    string PaymentAuthority,
    decimal TotalAmount,
    bool IsSplitBill,
    string TrackingCode,
    bool RequiresPayment = true,
    decimal WalletSpend = 0);

public class CreateOrderCheckoutCommandValidator : AbstractValidator<CreateOrderCheckoutCommand>
{
    public CreateOrderCheckoutCommandValidator()
    {
        RuleFor(c => c.CustomerName).NotEmpty().MaximumLength(200);
        RuleFor(c => c.CustomerPhone).NotEmpty().MaximumLength(30);
        RuleFor(c => c.Note).MaximumLength(500);
        RuleFor(c => c.DiscountCode).MaximumLength(64);
        RuleFor(c => c.IdempotencyKey).MaximumLength(100);
        RuleForEach(c => c.ExpectedLinePrices).ChildRules(snapshot =>
        {
            snapshot.RuleFor(s => s.MenuItemId).NotEmpty();
            snapshot.RuleFor(s => s.UnitPrice).GreaterThanOrEqualTo(0);
        });
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
    private readonly IOrderPaymentFinalizer _orderPaymentFinalizer;
    private readonly PickupSlotService _pickupSlotService;
    private readonly IPickupSlotReservationService _pickupSlotReservationService;

    public CreateOrderCheckoutCommandHandler(
        IApplicationDbContext dbContext,
        IPaymentGateway paymentGateway,
        IOptions<PaymentOptions> paymentOptions,
        IDiscountEngine discountEngine,
        IOrderPaymentFinalizer orderPaymentFinalizer,
        PickupSlotService pickupSlotService,
        IPickupSlotReservationService pickupSlotReservationService)
    {
        _dbContext = dbContext;
        _paymentGateway = paymentGateway;
        _paymentOptions = paymentOptions.Value;
        _discountEngine = discountEngine;
        _orderPaymentFinalizer = orderPaymentFinalizer;
        _pickupSlotService = pickupSlotService;
        _pickupSlotReservationService = pickupSlotReservationService;
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

        // Idempotency: a double-click or retried request carries the same key.
        // Returning a clear conflict keeps the customer from being charged for
        // (and the kitchen from preparing) a duplicate order.
        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            var idempotencyKey = request.IdempotencyKey.Trim();
            var existingOrder = await _dbContext.Orders
                .AsNoTracking()
                .FirstOrDefaultAsync(order => order.IdempotencyKey == idempotencyKey, cancellationToken);

            if (existingOrder is not null)
            {
                throw new CheckoutConflictException(
                    "duplicate_order",
                    $"این سفارش قبلاً ثبت شده است. کد پیگیری: {existingOrder.TrackingCode}",
                    new Dictionary<string, object?>
                    {
                        ["trackingCode"] = existingOrder.TrackingCode,
                        ["orderId"] = existingOrder.Id
                    });
            }
        }

        var pickupSettings = PickupSettingsSnapshot.From(siteSettings);
        if (request.DeliveryAt is not null && !pickupSettings.Enabled)
        {
            throw new CheckoutConflictException(
                "scheduling_disabled",
                "زمان‌بندی تحویل حضوری در حال حاضر فعال نیست؛ لطفاً سفارش را بدون زمان ثبت کنید.");
        }

        var splitBillMode = ResolveSplitBillMode(request);
        var isSplitBill = splitBillMode != SplitBillMode.Single;

        var menuItemIds = request.Items.Select(i => i.MenuItemId).Distinct().ToList();
        var modifierIds = request.Items.SelectMany(i => i.ModifierIds).Distinct().ToList();

        var allMenuItems = await _dbContext.MenuItems
            .Where(m => menuItemIds.Contains(m.Id))
            .ToListAsync(cancellationToken);

        var unavailableMenuItems = allMenuItems
            .Where(m => !m.IsActive || !m.IsOrderable)
            .Select(m => new Dictionary<string, object?>
            {
                ["menuItemId"] = m.Id,
                ["persianName"] = m.PersianName,
                ["reason"] = m.IsActive ? "not_orderable" : "inactive"
            })
            .ToList();

        var missingMenuItemIds = menuItemIds
            .Except(allMenuItems.Select(m => m.Id))
            .ToList();

        if (unavailableMenuItems.Count > 0 || missingMenuItemIds.Count > 0)
        {
            throw new CheckoutConflictException(
                "item_unavailable",
                "بعضی از محصولات سبد خرید دیگر قابل سفارش نیستند؛ لطفاً آن‌ها را حذف یا جایگزین کنید.",
                new Dictionary<string, object?>
                {
                    ["items"] = unavailableMenuItems
                        .Concat(missingMenuItemIds.Select(id => new Dictionary<string, object?>
                        {
                            ["menuItemId"] = id,
                            ["persianName"] = "محصول نامشخص",
                            ["reason"] = "not_found"
                        }))
                        .ToArray()
                });
        }

        var menuItems = allMenuItems;

        var modifiers = modifierIds.Count == 0
            ? []
            : await _dbContext.MenuModifiers
                .Where(m => modifierIds.Contains(m.Id) && m.IsActive)
                .ToListAsync(cancellationToken);

        var modifiersById = modifiers.ToDictionary(m => m.Id);

        var customDrinkItems = request.Items
            .Where(item => item.CustomDrink is not null)
            .ToList();

        var builderProfilesById = new Dictionary<Guid, DrinkBuilderProfile>();
        if (customDrinkItems.Count > 0)
        {
            if (siteSettings is { CustomDrinkBuilderEnabled: false })
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

        // Per-item option groups (TASK-013): definitions are loaded for every
        // item in the cart — not only for lines that sent option ids — so that
        // required groups are enforced even when the client sends nothing.
        var optionMenuItemIds = request.Items
            .Select(item => item.MenuItemId)
            .Distinct()
            .ToList();

        var optionGroupsByItem = new Dictionary<Guid, IReadOnlyList<MenuItemOptionGroup>>();
        if (optionMenuItemIds.Count > 0)
        {
            var optionGroups = await _dbContext.MenuItemOptionGroups
                .AsNoTracking()
                .Include(group => group.Options)
                .ThenInclude(option => option.Ingredients)
                .ThenInclude(ingredientRow => ingredientRow.Ingredient)
                .Where(group => optionMenuItemIds.Contains(group.MenuItemId) && group.IsActive)
                .OrderBy(group => group.DisplayOrder)
                .ToListAsync(cancellationToken);

            optionGroupsByItem = optionGroups
                .GroupBy(group => group.MenuItemId)
                .ToDictionary(group => group.Key, group => (IReadOnlyList<MenuItemOptionGroup>)group.ToList());
        }

        var resolvedLines = request.Items
            .Select(item =>
            {
                var menuItem = menuItems.First(m => m.Id == item.MenuItemId);
                var resolved = OrderPricing.ResolveLine(menuItem, item.SizeKey, item.Quantity, modifiersById, item.ModifierIds);

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
                        // Options always ride on the server-computed price: the
                        // customer pays base size + exact option deltas.
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

                    // The final price is always the server-computed component total
                    // added to the base size price (options already included).
                    // Client prices are display-only.
                    resolved = resolved with
                    {
                        UnitPrice = snapshot.UnitPrice,
                        ModifierLabels = resolved.ModifierLabels
                            .Concat(snapshot.SummaryLabels)
                            .ToArray(),
                    };
                }

                return (resolved: resolved, payerKey: item.PayerKey, payerLabel: item.PayerLabel, snapshot: snapshot, optionSnapshot: optionSnapshot, clientLineKey: item.ClientLineKey);
            })
            .ToList();

        var itemsTotal = resolvedLines.Sum(line => line.resolved.UnitPrice * line.resolved.Quantity);
        var taxTotal = resolvedLines.Sum(line =>
            Math.Round(line.resolved.UnitPrice * line.resolved.Quantity * line.resolved.TaxPercent / 100m, 2));
        var totalAmount = itemsTotal + taxTotal;

        // ── Pre-submit re-validation (TASK-012 §5) ───────────────────────
        // Everything below runs on the server, not only in the UI: a price or
        // stock change between the cart and the final click must never be
        // accepted silently.
        await EnsureLinesAreAvailableAsync(
            resolvedLines
                .Select(line => (line.resolved, line.optionSnapshot))
                .ToList(),
            cancellationToken);
        EnsurePricesHaveNotChanged(
            request,
            resolvedLines.Select(line => (line.resolved, line.clientLineKey)).ToList(),
            itemsTotal,
            totalAmount);

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
        var discountLines = new List<DiscountLineInput>();
        var requestedDiscountCode = string.IsNullOrWhiteSpace(request.DiscountCode)
            ? null
            : Discount.NormalizeCode(request.DiscountCode);

        // Abuse protection: a blacklisted phone or a temporarily locked identity
        // is rejected before the code is even evaluated, so brute-forcing is
        // stopped early. Thresholds come from live site settings.
        await DiscountAbuseGuard.EnsureAllowedAsync(
            _dbContext,
            siteSettings,
            request.CustomerPhone,
            request.IpAddress,
            cancellationToken);

        foreach (var (resolved, payerKey, payerLabel, _, optionSnapshot, _) in resolvedLines)
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

        var discountTotal = discountEvaluation.Applications.Sum(application => application.Amount);

        // A typed code that produced no application means the engine rejected it
        // (unknown / inactive / expired / out of schedule / below minimum cart /
        // wrong customer group / no eligible items / not stackable). Surface the
        // domain message to the customer instead of silently creating an order
        // without any discount.
        if (requestedDiscountCode is not null &&
            discountEvaluation.Applications.Count == 0 &&
            discountEvaluation.Rejections.Count > 0)
        {
            await DiscountAbuseGuard.RecordFailureAsync(
                _dbContext,
                siteSettings,
                request.CustomerPhone,
                requestedDiscountCode,
                request.IpAddress,
                cancellationToken);

            throw new DomainException(
                discountEvaluation.CodeRejection?.Message ?? "این کد تخفیف معتبر نیست.");
        }

        // Daily per-customer cap: only paid orders consume it, so this is the
        // allowance left before this order's benefits are applied.
        var remainingDailyCap = await DiscountDailyCapService.GetRemainingAllowanceAsync(
            _dbContext,
            siteSettings,
            customer?.Id,
            siteSettings?.TimeZoneId,
            DateTimeOffset.UtcNow,
            cancellationToken: cancellationToken);

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

        foreach (var (resolved, payerKey, payerLabel, snapshot, optionSnapshot, _) in resolvedLines)
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
                resolved.TaxPercent,
                snapshot is null ? null : DrinkBuilderPricing.Serialize(snapshot),
                snapshot?.SummaryLabels,
                optionSnapshot is { Components.Count: > 0 } ? optionSnapshot.Labels : null,
                optionSnapshot is { Components.Count: > 0 } ? MenuItemOptionPricing.Serialize(optionSnapshot) : null));
        }

        order.RecalculateTotal();

        // Every benefit below flows through the tracker: it clamps the amount to
        // the remaining daily allowance, applies it to the order and records the
        // discount rows (split by category) used by reports and the cap.
        var discountTracker = new OrderDiscountTracker(
            order,
            customer?.Id,
            remainingDailyCap,
            _dbContext.OrderDiscounts);
        var categoryByMenuItem = resolvedLines
            .GroupBy(line => line.resolved.MenuItemId)
            .ToDictionary(group => group.Key, group => (Guid?)group.First().resolved.CategoryId);

        var adjustedApplications = new List<DiscountApplication>();
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
                adjustedApplications.Add(application with { Amount = applied });
            }
        }

        // The code was valid but the customer already used their daily discount
        // allowance: fail clearly instead of creating a full-price order.
        if (requestedDiscountCode is not null &&
            discountEvaluation.Applications.Count > 0 &&
            adjustedApplications.Count == 0)
        {
            throw new DomainException(
                "سقف تخفیف روزانهٔ شما تکمیل شده است؛ لطفاً فردا دوباره تلاش کنید.");
        }

        if (requestedDiscountCode is not null && adjustedApplications.Count > 0)
        {
            await DiscountAbuseGuard.RecordSuccessAsync(
                _dbContext,
                siteSettings,
                request.CustomerPhone,
                requestedDiscountCode,
                request.IpAddress,
                cancellationToken);
        }

        discountTotal = discountTracker.AppliedTotal;

        if (discountTotal > 0)
        {
            await ApplyReferralRewardAsync(customer, request.DiscountCode, cancellationToken);
        }
        if (request.RedeemPoints > 0 && customer is not null && siteSettings is { LoyaltyEnabled: true } &&
            siteSettings.LoyaltyRedeemTomanPerPoint > 0)
        {
            var points = Math.Min(request.RedeemPoints, customer.LoyaltyPoints);
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
                        $"مصرف امتیاز در سفارش {order.TrackingCode}",
                        order.Id,
                        null));
                }
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
                    discountTracker.Apply(OrderDiscountKind.StampCard, "پاداش کارت مهر", stampDiscount);
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
                    discountTracker.Apply(OrderDiscountKind.Tier, tier.Name, tierDiscount);
                }
            }
        }

        // Birthday benefit (auto mode): while the customer is inside their
        // birthday window the percent drops in automatically — on top of
        // standing and manual discounts. Code-mode birthdays instead rely on
        // a personal code issued through the regular discount engine.
        if (customer is not null &&
            siteSettings is { BirthdayDiscountEnabled: true, BirthdayDiscountMode: BirthdayDiscountMode.Automatic } &&
            siteSettings.BirthdayDiscountPercent > 0 &&
            customer.DateOfBirth is not null &&
            BirthdayDiscountService.IsBirthdayActive(
                customer.DateOfBirth.Value, siteSettings.BirthdayDiscountWindowDays))
        {
            var birthdayDiscount = Math.Round(itemsTotal * siteSettings.BirthdayDiscountPercent / 100m, 2);
            if (birthdayDiscount > 0)
            {
                discountTracker.Apply(OrderDiscountKind.Birthday, "هدیهٔ تولد", birthdayDiscount);
            }
        }

        // Customer group: the standing discount of the customer's best active
        // membership applies automatically (highest-percent group wins).
        if (customer is not null)
        {
            var groupMembership = await CustomerGroupService.GetActiveMembershipAsync(
                _dbContext, customer.Id, cancellationToken);
            if (groupMembership is { } membership && membership.Group.DiscountPercent > 0)
            {
                var groupDiscount = Math.Round(itemsTotal * membership.Group.DiscountPercent / 100m, 2);
                if (groupDiscount > 0)
                {
                    discountTracker.Apply(OrderDiscountKind.Group, membership.Group.Name, groupDiscount);
                }
            }
        }

        // Personal cup (لیوان شخصی): a fixed discount on the covered categories
        // only, capped at the value of the eligible items.
        if (request.PersonalCup && customer is not null &&
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

        // Monthly subscription: redeem eligible units (cheapest first, bounded
        // by the remaining count and the daily cap) and subtract their value.
        // A fully-discounted order must not burn uses for no benefit.
        if (request.UseSubscription && customer is not null && order.TotalAmount > 0)
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

                if (plan.TotalUnits > 0)
                {
                    var unitsToConsume = 0;
                    foreach (var unit in plan.Units)
                    {
                        var quantity = unit.Quantity;

                        // Only whole units that fit inside the remaining daily
                        // allowance are redeemed — never a partial unit.
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
                        await _dbContext.SaveChangesAsync(cancellationToken);
                    }
                }
            }
        }

        order.AssignCustomer(customer?.Id);

        order.ApplyDeliveryTime(request.DeliveryAt);
        order.SetIdempotencyKey(request.IdempotencyKey);

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

        // Slot validation + capacity reservation. The pre-check gives the
        // customer a fast, friendly error; the locked re-check guarantees that
        // two simultaneous checkouts can never take the last place.
        IPickupSlotReservation? slotReservation = null;
        try
        {
            if (request.DeliveryAt is { } requestedDeliveryAt)
            {
                var preCheck = await _pickupSlotService.ValidateAsync(requestedDeliveryAt, cancellationToken);
                if (!preCheck.IsValid)
                {
                    throw BuildSlotConflict(preCheck);
                }

                var timeZone = PickupScheduleCalculator.ResolveTimeZone(pickupSettings.TimeZoneId);
                var openHours = await _pickupSlotService.GetOpenHoursAsync(cancellationToken);

                slotReservation = await _pickupSlotReservationService.ReserveAsync(
                    requestedDeliveryAt,
                    cancellationToken);

                var lockedCheck = await _pickupSlotService.ValidateCapacityOnlyAsync(
                    requestedDeliveryAt,
                    pickupSettings,
                    openHours,
                    timeZone,
                    cancellationToken);

                if (!lockedCheck.IsValid)
                {
                    throw BuildSlotConflict(lockedCheck);
                }
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            if (slotReservation is not null)
            {
                await slotReservation.CommitAsync(cancellationToken);
            }
        }
        catch (DbUpdateException exception) when (IsDuplicateIdempotencyKey(exception))
        {
            throw new CheckoutConflictException(
                "duplicate_order",
                "این سفارش در حال ثبت است یا قبلاً ثبت شده است؛ لطفاً صفحه را دوباره بارگذاری کنید.",
                new Dictionary<string, object?> { ["trackingCode"] = null });
        }
        finally
        {
            if (slotReservation is not null)
            {
                await slotReservation.DisposeAsync();
            }
        }

        if (discountTotal > 0)
        {
            await _discountEngine.RecordRedemptionsAsync(
                order.Id,
                customer?.Id,
                adjustedApplications,
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

        // Wallet payment: spend the customer's balance against the remaining
        // payable before further payment is requested. Money leaves the wallet
        // exactly once — logged as one Purchase transaction on the order.
        var walletSpend = 0m;
        if (request.UseWallet && customer is not null && siteSettings is { WalletEnabled: true })
        {
            var wallet = await WalletService.GetOrCreateAsync(_dbContext, customer.Id, cancellationToken);

            // Enforce cashback expiry first, so the spend works against the
            // genuinely available (non-expired) balance.
            await WalletService.ExpireDueCashbackAsync(_dbContext, wallet, cancellationToken);

            walletSpend = await WalletService.SpendForOrderAsync(
                _dbContext,
                wallet,
                order,
                order.TotalAmount,
                siteSettings.WalletPaymentMaxPercent,
                cancellationToken);
            if (walletSpend > 0)
            {
                order.ApplyWalletPayment(walletSpend);
            }
        }

        if (walletSpend > 0 && order.TotalAmount <= 0)
        {
            // Fully covered by the wallet: finalize immediately through the
            // shared pipeline (inventory, accounting, receipts, loyalty chain).
            await _dbContext.SaveChangesAsync(cancellationToken);
            await _orderPaymentFinalizer.FinalizePaidOrderAsync(order, cancellationToken);

            var walletSuccessUrl = $"{_paymentOptions.PublicBaseUrl.TrimEnd('/')}/?order=success&code={order.TrackingCode}";
            return new CreateOrderCheckoutResultDto(
                order.Id,
                walletSuccessUrl,
                string.Empty,
                0m,
                false,
                order.TrackingCode,
                RequiresPayment: false,
                walletSpend);
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
            order.TrackingCode,
            RequiresPayment: true,
            walletSpend);
    }

    private static CheckoutConflictException BuildSlotConflict(PickupValidationResult result) =>
        new(
            result.Code ?? "slot_unavailable",
            result.Message ?? "بازهٔ انتخاب‌شده معتبر نیست؛ لطفاً بازهٔ دیگری انتخاب کنید.",
            new Dictionary<string, object?>
            {
                ["reason"] = result.Code,
                ["nearestSlot"] = result.NearestAvailableSlot
            });

    private static bool IsDuplicateIdempotencyKey(DbUpdateException exception) =>
        exception.InnerException?.Message.Contains("IdempotencyKey", StringComparison.OrdinalIgnoreCase) == true ||
        exception.Message.Contains("IdempotencyKey", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Stock re-check before the order is accepted. A product that sold out
    /// between the cart and the final click is reported by name so the theme
    /// can offer "remove" / "replace" instead of a generic failure. The exact
    /// ingredient usage of the chosen per-item options is checked here too.
    /// </summary>
    private async Task EnsureLinesAreAvailableAsync(
        IReadOnlyCollection<(OrderPricing.ResolvedLine resolved, MenuItemOptionSnapshot? options)> lines,
        CancellationToken cancellationToken)
    {
        var menuItemIds = lines.Select(line => line.resolved.MenuItemId).Distinct().ToList();
        var stockByMenuItem = await _dbContext.MenuItemStocks
            .AsNoTracking()
            .Where(stock => menuItemIds.Contains(stock.MenuItemId))
            .ToDictionaryAsync(stock => stock.MenuItemId, cancellationToken);

        var requestedByMenuItem = lines
            .GroupBy(line => line.resolved.MenuItemId)
            .ToDictionary(group => group.Key, group => group.Sum(line => line.resolved.Quantity));

        var unavailable = new List<Dictionary<string, object?>>();
        foreach (var line in lines)
        {
            var requested = requestedByMenuItem.GetValueOrDefault(line.resolved.MenuItemId);
            if (!stockByMenuItem.TryGetValue(line.resolved.MenuItemId, out var stock))
            {
                continue;
            }

            if (stock.CurrentQuantity < requested)
            {
                unavailable.Add(new Dictionary<string, object?>
                {
                    ["menuItemId"] = line.resolved.MenuItemId,
                    ["persianName"] = line.resolved.PersianName,
                    ["reason"] = "insufficient_stock",
                    ["available"] = stock.CurrentQuantity,
                    ["requested"] = requested
                });
            }
        }

        await EnsureOptionIngredientsAreAvailableAsync(lines, unavailable, cancellationToken);

        if (unavailable.Count > 0)
        {
            throw new CheckoutConflictException(
                "item_unavailable",
                "موجودی بعضی از محصولات سبد خرید کافی نیست؛ لطفاً آن‌ها را حذف یا جایگزین کنید.",
                new Dictionary<string, object?>
                {
                    ["items"] = unavailable.ToArray()
                });
        }
    }

    /// <summary>
    /// Verifies every selected option's ingredient demand (usage × quantity,
    /// aggregated across the whole cart) against current stock; unlike the base
    /// recipes, options can run out individually so the customer is told which
    /// option is the problem.
    /// </summary>
    private async Task EnsureOptionIngredientsAreAvailableAsync(
        IReadOnlyCollection<(OrderPricing.ResolvedLine resolved, MenuItemOptionSnapshot? options)> lines,
        List<Dictionary<string, object?>> unavailable,
        CancellationToken cancellationToken)
    {
        var demandByIngredient = new Dictionary<Guid, decimal>();
        foreach (var (resolved, options) in lines)
        {
            if (options is null)
            {
                continue;
            }

            foreach (var usage in options.Ingredients)
            {
                demandByIngredient[usage.IngredientId] =
                    demandByIngredient.GetValueOrDefault(usage.IngredientId)
                    + usage.Quantity * resolved.Quantity;
            }
        }

        if (demandByIngredient.Count == 0)
        {
            return;
        }

        var optionStocks = await _dbContext.InventoryStocks
            .AsNoTracking()
            .Where(stock => demandByIngredient.Keys.Contains(stock.IngredientId))
            .ToDictionaryAsync(stock => stock.IngredientId, cancellationToken);

        foreach (var (resolved, options) in lines)
        {
            if (options is null)
            {
                continue;
            }

            foreach (var usage in options.Ingredients)
            {
                var requested = usage.Quantity * resolved.Quantity;
                var stock = optionStocks.GetValueOrDefault(usage.IngredientId);
                if (stock is not null && stock.CurrentQuantity >= requested)
                {
                    continue;
                }

                var optionNames = string.Join("، ", options.Labels);
                unavailable.Add(new Dictionary<string, object?>
                {
                    ["menuItemId"] = resolved.MenuItemId,
                    ["persianName"] = resolved.PersianName,
                    ["reason"] = "insufficient_option_ingredient",
                    ["option"] = usage.IngredientPersianName,
                    ["selectedOptions"] = optionNames,
                    ["available"] = stock?.CurrentQuantity ?? 0,
                    ["requested"] = requested
                });
            }
        }
    }

    /// <summary>
    /// Price re-check: the client sends the unit price it displayed. Any change
    /// aborts the checkout with a structured conflict so the customer must
    /// explicitly confirm the new price — silent re-pricing is forbidden.
    /// </summary>
    private static void EnsurePricesHaveNotChanged(
        CreateOrderCheckoutCommand request,
        IReadOnlyCollection<(OrderPricing.ResolvedLine resolved, string? clientLineKey)> lines,
        decimal itemsTotal,
        decimal totalAmount)
    {
        if (request.AcceptPriceChanges)
        {
            return;
        }

        var changes = new List<Dictionary<string, object?>>();

        if (request.ExpectedLinePrices is { Count: > 0 })
        {
            var expectedByKey = request.ExpectedLinePrices
                .Where(snapshot => !string.IsNullOrWhiteSpace(snapshot.ClientLineKey))
                .GroupBy(snapshot => snapshot.ClientLineKey!)
                .ToDictionary(group => group.Key, group => group.First().UnitPrice);

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line.clientLineKey) ||
                    !expectedByKey.TryGetValue(line.clientLineKey, out var expectedUnitPrice))
                {
                    continue;
                }

                if (Math.Abs(expectedUnitPrice - line.resolved.UnitPrice) > 0.01m)
                {
                    changes.Add(new Dictionary<string, object?>
                    {
                        ["clientLineKey"] = line.clientLineKey,
                        ["menuItemId"] = line.resolved.MenuItemId,
                        ["persianName"] = line.resolved.PersianName,
                        ["oldUnitPrice"] = expectedUnitPrice,
                        ["newUnitPrice"] = line.resolved.UnitPrice,
                        ["quantity"] = line.resolved.Quantity
                    });
                }
            }
        }

        var totalChanged = request.ExpectedTotalAmount is { } expectedTotal &&
                           Math.Abs(expectedTotal - itemsTotal) > 0.01m;

        if (changes.Count == 0 && !totalChanged)
        {
            return;
        }

        throw new CheckoutConflictException(
            "price_changed",
            "قیمت بعضی از محصولات تغییر کرده است؛ لطفاً قیمت جدید را تأیید کنید.",
            new Dictionary<string, object?>
            {
                ["changes"] = changes.ToArray(),
                ["oldItemsTotal"] = request.ExpectedTotalAmount,
                ["newItemsTotal"] = itemsTotal,
                ["newTotal"] = totalAmount
            });
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
