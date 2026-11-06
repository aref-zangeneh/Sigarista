using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Accounting.Services;
using Sugarista.Application.Customers.Services;
using Sugarista.Application.DeliveryMethods.Services;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.Discounts.Security;
using Sugarista.Application.DrinkBuilder;
using Sugarista.Application.Orders;
using Sugarista.Application.Orders.Commands.CreateOrderCheckout;
using Sugarista.Application.Orders.Services;
using Sugarista.Domain.Entities;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Application.Pos.Commands.UpsertPosOrder;

public enum PosOrderAction
{
    Hold,
    PayCash,
    PayCard
}

public record PosOrderLineRequest(
    Guid MenuItemId,
    string? SizeKey,
    int Quantity,
    IReadOnlyCollection<Guid> ModifierIds,
    string? BaristaNote = null,
    string? PayerKey = null,
    string? PayerLabel = null,
    DrinkBuilderLineRequest? CustomDrink = null);

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
    bool UseSubscription = false) : IRequest<UpsertPosOrderResultDto>;

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
            .WithMessage("Action must be Hold, PayCash, or PayCard.");
        RuleFor(c => c.Note).MaximumLength(500);
    }
}

public class UpsertPosOrderCommandHandler : IRequestHandler<UpsertPosOrderCommand, UpsertPosOrderResultDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IAccountingService _accountingService;
    private readonly IPrintJobService _printJobService;

    public UpsertPosOrderCommandHandler(
        IApplicationDbContext dbContext,
        IAccountingService accountingService,
        IPrintJobService printJobService)
    {
        _dbContext = dbContext;
        _accountingService = accountingService;
        _printJobService = printJobService;
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

        ValidateSplitBill(request, splitBillMode, totalAmount);

        var tableSessionId = await ResolveTableSessionIdAsync(request.SalonTableId, serviceType, cancellationToken);

        Order order;
        if (request.OrderId.HasValue)
        {
            order = await _dbContext.Orders
                .Include(o => o.Lines)
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
                request.CustomerName,
                request.CustomerPhone,
                request.Note,
                totalAmount,
                serviceType,
                request.SalonTableId,
                tableSessionId,
                request.ServedByAdminUserId,
                splitBillMode,
                request.GuestCount);

            foreach (var (resolved, lineRequest, snapshot) in resolvedLines)
            {
                order.AddLine(CreateLine(order.Id, resolved, lineRequest, snapshot));
            }

            if (isSplitBill && request.SplitShares is { Count: > 0 })
            {
                AddSplitShares(order, request.SplitShares);
            }

            _dbContext.Orders.Add(order);
        }

        order.UpdateCustomerForPos(request.CustomerName, request.CustomerPhone, request.Note);
        order.ConfigurePos(
            OrderChannel.Pos,
            serviceType,
            request.SalonTableId,
            tableSessionId,
            request.ServedByAdminUserId);

        order.ApplyDeliveryMethod(deliveryMethod?.Id, deliveryFee);
        order.AssignCustomer(customer?.Id);

        // Loyalty-style benefits for the POS channel: customer-group standing
        // discount and the personal-cup fixed discount are recomputed on every
        // upsert (the total is recalculated first, so they never stack twice).
        var siteSettings = await _dbContext.SiteSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        // Recomputing an existing order replaces its discount rows; the daily
        // allowance is calculated excluding this order so an edit cannot spend
        // the customer's cap twice.
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

        if (customer is not null)
        {
            var groupMembership = await CustomerGroupService.GetActiveMembershipAsync(
                _dbContext, customer.Id, cancellationToken);
            if (groupMembership is { } membership && membership.Group.DiscountPercent > 0)
            {
                var groupDiscount = Math.Round(itemsTotal * membership.Group.DiscountPercent / 100m, 2);
                if (groupDiscount > 0)
                {
                    discountTracker.Apply(
                        OrderDiscountKind.Group,
                        membership.Group.Name,
                        groupDiscount);
                }
            }
        }

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

        // Monthly subscription: consumed exactly once, at the moment the order
        // is actually paid (holding an order must not burn the customer's uses).
        // A fully-discounted order must not burn uses for no benefit.
        if (request.UseSubscription && customer is not null && order.TotalAmount > 0 &&
            action is PosOrderAction.PayCash or PosOrderAction.PayCard)
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

                var categoryByMenuItem = resolvedLines
                    .GroupBy(line => line.resolved.MenuItemId)
                    .ToDictionary(group => group.Key, group => (Guid?)group.First().resolved.CategoryId);

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

        switch (action)
        {
            case PosOrderAction.Hold:
                order.Hold();
                break;
            case PosOrderAction.PayCash:
                order.PayAtPos(PosPaymentMethod.Cash);
                await OrderInventoryService.DeductInventoryForOrderLinesAsync(
                    _dbContext,
                    order,
                    order.Lines.ToList(),
                    cancellationToken);
                await DeliveryMethodInventoryService.DeductForOrderAsync(_dbContext, order, cancellationToken);
                break;
            case PosOrderAction.PayCard:
                order.PayAtPos(PosPaymentMethod.Card);
                await OrderInventoryService.DeductInventoryForOrderLinesAsync(
                    _dbContext,
                    order,
                    order.Lines.ToList(),
                    cancellationToken);
                await DeliveryMethodInventoryService.DeductForOrderAsync(_dbContext, order, cancellationToken);
                break;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (action is PosOrderAction.PayCash or PosOrderAction.PayCard)
        {
            await _accountingService.PostOrderPaymentAsync(order, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            // Queue the thermal receipt copies (customer + barista) for the print agent.
            await _printJobService.EnqueueOrderReceiptsAsync(order, cancellationToken);
        }

        return new UpsertPosOrderResultDto(
            order.Id,
            order.TrackingCode,
            order.Status.ToString(),
            order.TotalAmount,
            order.PaymentMethod?.ToString());
    }

    private async Task ReplaceLinesAsync(
        Order order,
        IReadOnlyCollection<(OrderPricing.ResolvedLine resolved, PosOrderLineRequest request, DrinkBuilderSnapshot? snapshot)> resolvedLines,
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

        foreach (var (resolved, lineRequest, snapshot) in resolvedLines)
        {
            var line = CreateLine(order.Id, resolved, lineRequest, snapshot);
            order.AddLine(line);
            _dbContext.OrderLines.Add(line);
        }
    }

    private static OrderLine CreateLine(
        Guid orderId,
        OrderPricing.ResolvedLine resolved,
        PosOrderLineRequest lineRequest,
        DrinkBuilderSnapshot? snapshot) =>
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
            snapshot?.SummaryLabels);

    private async Task<IReadOnlyCollection<(OrderPricing.ResolvedLine resolved, PosOrderLineRequest request, DrinkBuilderSnapshot? snapshot)>> ResolveLinesAsync(
        IReadOnlyCollection<PosOrderLineRequest> items,
        CancellationToken cancellationToken)
    {
        var menuItemIds = items.Select(item => item.MenuItemId).Distinct().ToList();
        var modifierIds = items.SelectMany(item => item.ModifierIds).Distinct().ToList();

        var menuItems = await _dbContext.MenuItems
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

                return (resolved, item, snapshot);
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
