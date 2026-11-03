using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.Orders;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Carts.Commands.TrackCart;

public record TrackCartItemRequest(
    Guid MenuItemId,
    string? SizeKey,
    int Quantity,
    IReadOnlyCollection<Guid> ModifierIds);

/// <summary>
/// Storefront heartbeat for the server-side cart mirror. The browser owns the
/// cart; this command lets the reminder sweep see a cart that was left behind,
/// together with the contact details the customer typed at checkout.
/// </summary>
public record TrackCartCommand(
    string CartToken,
    string? CustomerName,
    string? PhoneNumber,
    string? Email,
    IReadOnlyCollection<TrackCartItemRequest> Items) : IRequest<TrackCartResultDto>;

public record TrackCartResultDto(bool Tracked, decimal ItemsTotal, int ItemCount);

public class TrackCartCommandValidator : AbstractValidator<TrackCartCommand>
{
    public TrackCartCommandValidator()
    {
        RuleFor(command => command.CartToken).NotEmpty().MaximumLength(64);
        RuleFor(command => command.CustomerName).MaximumLength(200);
        RuleFor(command => command.PhoneNumber).MaximumLength(30);
        RuleFor(command => command.Email)
            .MaximumLength(200)
            .EmailAddress()
            .When(command => !string.IsNullOrWhiteSpace(command.Email));
        RuleFor(command => command.Items).NotNull();
        RuleFor(command => command.Items.Count).LessThanOrEqualTo(100);
        RuleForEach(command => command.Items).ChildRules(item =>
        {
            item.RuleFor(entry => entry.MenuItemId).NotEmpty();
            item.RuleFor(entry => entry.Quantity).GreaterThan(0);
        });
    }
}

public class TrackCartCommandHandler : IRequestHandler<TrackCartCommand, TrackCartResultDto>
{
    private readonly IApplicationDbContext _dbContext;

    public TrackCartCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<TrackCartResultDto> Handle(TrackCartCommand request, CancellationToken cancellationToken)
    {
        if (request.Items.Count == 0)
        {
            await ExpireOpenCartAsync(request.CartToken, cancellationToken);
            return new TrackCartResultDto(false, 0m, 0);
        }

        var menuItemIds = request.Items.Select(item => item.MenuItemId).Distinct().ToList();
        var modifierIds = request.Items
            .SelectMany(item => item.ModifierIds ?? [])
            .Distinct()
            .ToList();

        var menuItems = await _dbContext.MenuItems
            .Where(item => menuItemIds.Contains(item.Id) && item.IsActive)
            .ToListAsync(cancellationToken);

        if (menuItems.Count == 0)
        {
            await ExpireOpenCartAsync(request.CartToken, cancellationToken);
            return new TrackCartResultDto(false, 0m, 0);
        }

        var modifiers = modifierIds.Count == 0
            ? []
            : await _dbContext.MenuModifiers
                .Where(modifier => modifierIds.Contains(modifier.Id) && modifier.IsActive)
                .ToListAsync(cancellationToken);

        var modifiersById = modifiers.ToDictionary(modifier => modifier.Id);

        // Price the snapshot on the server (never trust client totals) and drop
        // items that disappeared or became unavailable since the cart was built.
        var resolvedLines = request.Items
            .Select(item =>
            {
                var menuItem = menuItems.FirstOrDefault(entry => entry.Id == item.MenuItemId);
                if (menuItem is null)
                {
                    return null;
                }

                try
                {
                    return OrderPricing.ResolveLine(menuItem, item.SizeKey, item.Quantity, modifiersById, item.ModifierIds ?? []);
                }
                catch (InvalidOperationException)
                {
                    return null;
                }
            })
            .Where(line => line is not null)
            .Select(line => line!)
            .ToList();

        if (resolvedLines.Count == 0)
        {
            await ExpireOpenCartAsync(request.CartToken, cancellationToken);
            return new TrackCartResultDto(false, 0m, 0);
        }

        var itemsTotal = resolvedLines.Sum(line => line.UnitPrice * line.Quantity);
        var itemCount = resolvedLines.Sum(line => line.Quantity);
        var itemsJson = JsonSerializer.Serialize(resolvedLines.Select(line => new
        {
            menuItemId = line.MenuItemId,
            persianName = line.PersianName,
            sizeKey = line.SizeKey,
            quantity = line.Quantity,
            unitPrice = line.UnitPrice
        }));

        var phone = NormalizePhone(request.PhoneNumber);
        var customer = phone is null
            ? null
            : await _dbContext.Customers
                .AsNoTracking()
                .FirstOrDefaultAsync(entry => entry.PhoneNumber == phone, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var token = request.CartToken.Trim();

        var activeCart = await _dbContext.AbandonedCarts
            .Where(cart => cart.CartToken == token &&
                (cart.Status == AbandonedCartStatus.Active || cart.Status == AbandonedCartStatus.Reminded))
            .OrderByDescending(cart => cart.LastActivityAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (activeCart is null)
        {
            _dbContext.AbandonedCarts.Add(new AbandonedCart(
                token,
                request.CustomerName,
                phone,
                request.Email,
                customer?.Id,
                itemsJson,
                itemsTotal,
                itemCount,
                now));
        }
        else
        {
            activeCart.Touch(
                request.CustomerName,
                phone,
                request.Email,
                customer?.Id,
                itemsJson,
                itemsTotal,
                itemCount,
                now);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new TrackCartResultDto(true, itemsTotal, itemCount);
    }

    private static string? NormalizePhone(string? phone)
        => string.IsNullOrWhiteSpace(phone) ? null : phone.Replace(" ", string.Empty).Trim();

    /// <summary>
    /// The customer emptied the cart (or every line became unavailable), so the
    /// mirrored cart must not trigger a reminder later.
    /// </summary>
    private async Task ExpireOpenCartAsync(string cartToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(cartToken))
        {
            return;
        }

        var token = cartToken.Trim();

        var openCart = await _dbContext.AbandonedCarts
            .Where(cart => cart.CartToken == token &&
                (cart.Status == AbandonedCartStatus.Active || cart.Status == AbandonedCartStatus.Reminded))
            .OrderByDescending(cart => cart.LastActivityAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (openCart is null)
        {
            return;
        }

        openCart.MarkExpired();
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
