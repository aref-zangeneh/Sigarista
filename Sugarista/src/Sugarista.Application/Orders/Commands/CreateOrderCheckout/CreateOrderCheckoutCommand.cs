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
    IReadOnlyCollection<Guid> ModifierIds);

public record CreateOrderCheckoutCommand(
    string CustomerName,
    string CustomerPhone,
    string? Note,
    IReadOnlyCollection<CheckoutLineRequest> Items) : IRequest<CreateOrderCheckoutResultDto>;

public record CreateOrderCheckoutResultDto(
    Guid OrderId,
    string RedirectUrl,
    string PaymentAuthority,
    int TotalAmount);

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
                return OrderPricing.ResolveLine(menuItem, item.SizeKey, item.Quantity, modifiersById, item.ModifierIds);
            })
            .ToList();

        var totalAmount = resolvedLines.Sum(line => line.UnitPrice * line.Quantity);
        var trackingCode = await GenerateTrackingCodeAsync(cancellationToken);
        var paymentAuthority = Guid.NewGuid().ToString("N");

        var order = new Order(
            trackingCode,
            request.CustomerName,
            request.CustomerPhone,
            request.Note,
            totalAmount,
            paymentAuthority);

        foreach (var line in resolvedLines)
        {
            order.AddLine(new OrderLine(
                order.Id,
                line.MenuItemId,
                line.PersianName,
                line.EnglishName,
                line.SizeKey,
                line.SizeLabel,
                line.Quantity,
                line.UnitPrice,
                line.ModifierLabels));
        }

        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var callbackUrl = $"{_paymentOptions.PublicBaseUrl.TrimEnd('/')}/?pay={order.Id}&authority={paymentAuthority}";
        var payment = await _paymentGateway.RequestPaymentAsync(
            order.Id,
            order.TotalAmount,
            callbackUrl,
            $"Sugarista order {order.TrackingCode}",
            cancellationToken);

        order.UpdatePaymentAuthority(payment.Authority);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new CreateOrderCheckoutResultDto(order.Id, payment.RedirectUrl, payment.Authority, order.TotalAmount);
    }

    private async Task<string> GenerateTrackingCodeAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var code = $"SG-{Random.Shared.Next(1000, 9999)}";
            var exists = await _dbContext.Orders.AnyAsync(o => o.TrackingCode == code, cancellationToken);
            if (!exists)
            {
                return code;
            }
        }

        return $"SG-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
    }
}
