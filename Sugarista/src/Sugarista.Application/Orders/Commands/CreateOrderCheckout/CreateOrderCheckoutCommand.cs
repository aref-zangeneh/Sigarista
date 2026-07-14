using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.Common.Options;
using Sugarista.Application.Orders;
using Sugarista.Domain.Entities;

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
    IReadOnlyCollection<CheckoutSplitShareRequest>? SplitShares = null) : IRequest<CreateOrderCheckoutResultDto>;

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

    public CreateOrderCheckoutCommandHandler(
        IApplicationDbContext dbContext,
        IPaymentGateway paymentGateway,
        IOptions<PaymentOptions> paymentOptions)
    {
        _dbContext = dbContext;
        _paymentGateway = paymentGateway;
        _paymentOptions = paymentOptions.Value;
    }

    public async Task<CreateOrderCheckoutResultDto> Handle(CreateOrderCheckoutCommand request, CancellationToken cancellationToken)
    {
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

        var totalAmount = resolvedLines.Sum(line => line.resolved.UnitPrice * line.resolved.Quantity);

        if (splitBillMode == SplitBillMode.ByCustomAmount)
        {
            var shareTotal = request.SplitShares!.Sum(share => share.ShareAmount ?? 0);
            if (shareTotal != totalAmount)
            {
                throw new InvalidOperationException("Custom share amounts must sum to the order total.");
            }
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
                payerLabel));
        }

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
