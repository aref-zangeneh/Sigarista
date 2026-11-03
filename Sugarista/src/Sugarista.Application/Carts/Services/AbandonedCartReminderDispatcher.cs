using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Carts.Services;

/// <summary>
/// Sends the abandoned-cart reminder (SMS and/or email) to carts that stayed
/// without a paid order for longer than the admin-configured window. Every
/// reminder carries a single-use percentage code that expires after the
/// configured number of hours. All thresholds, channels, discount and message
/// wording live in <see cref="SiteSettings"/> so the admin fully controls it.
/// </summary>
public class AbandonedCartReminderDispatcher
{
    private const int BatchSize = 50;
    private const string CodePrefix = "CART";

    private static readonly JsonSerializerOptions SnapshotOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IApplicationDbContext _dbContext;
    private readonly IMaintenanceSmsSender _smsSender;
    private readonly IMaintenanceEmailSender _emailSender;
    private readonly ILogger<AbandonedCartReminderDispatcher> _logger;

    public AbandonedCartReminderDispatcher(
        IApplicationDbContext dbContext,
        IMaintenanceSmsSender smsSender,
        IMaintenanceEmailSender emailSender,
        ILogger<AbandonedCartReminderDispatcher> logger)
    {
        _dbContext = dbContext;
        _smsSender = smsSender;
        _emailSender = emailSender;
        _logger = logger;
    }

    /// <summary>
    /// Sends one reminder per due cart and returns how many were delivered.
    /// Carts that already ended in a paid order are marked converted and never
    /// receive a message.
    /// </summary>
    public async Task<int> DispatchDueRemindersAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _dbContext.SiteSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        if (settings is null || !settings.AbandonedCartReminderEnabled)
        {
            return 0;
        }

        var windowHours = settings.AbandonedCartHours;
        if (windowHours <= 0)
        {
            // Admin disabled the sweep by setting the inactivity window to zero.
            return 0;
        }

        var smsEnabled = settings.AbandonedCartSmsEnabled;
        var emailEnabled = settings.AbandonedCartEmailEnabled;
        if (!smsEnabled && !emailEnabled)
        {
            return 0;
        }

        var now = DateTimeOffset.UtcNow;
        var cutoff = now.AddHours(-windowHours);

        var dueCarts = await _dbContext.AbandonedCarts
            .Where(cart => cart.Status == AbandonedCartStatus.Active)
            .Where(cart => cart.ReminderSentAt == null)
            .Where(cart => cart.LastActivityAt <= cutoff)
            .Where(cart =>
                (smsEnabled && cart.PhoneNumber != null) ||
                (emailEnabled && cart.Email != null))
            .OrderBy(cart => cart.LastActivityAt)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        if (dueCarts.Count == 0)
        {
            return 0;
        }

        var paidOrders = await LoadPaidOrdersAsync(dueCarts, cancellationToken);

        var validityHours = Math.Clamp(settings.AbandonedCartCodeValidityHours, 1, 720);
        var percent = Math.Clamp(settings.AbandonedCartDiscountPercent, 0m, 100m);
        var sentCount = 0;
        var reservedCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var cart in dueCarts)
        {
            if (IsConverted(cart, paidOrders))
            {
                cart.MarkConverted(now);
                continue;
            }

            var code = await GenerateUniqueCodeAsync(reservedCodes, cancellationToken);
            reservedCodes.Add(code);
            var discount = CreateReminderDiscount(code, percent, validityHours, now);
            _dbContext.Discounts.Add(discount);

            var values = BuildTemplateValues(cart, settings, code, percent, validityHours);
            var channels = new List<string>();

            if (smsEnabled && !string.IsNullOrWhiteSpace(cart.PhoneNumber))
            {
                var message = AbandonedCartMessageTemplate.Render(settings.AbandonedCartSmsTemplate, values);

                if (!string.IsNullOrWhiteSpace(message))
                {
                    try
                    {
                        await _smsSender.SendAsync(cart.PhoneNumber, message, cancellationToken);
                        channels.Add("sms");
                    }
                    catch (Exception exception)
                    {
                        _logger.LogError(
                            exception,
                            "Failed to send abandoned-cart SMS for cart {CartId}.",
                            cart.Id);
                    }
                }
            }

            if (emailEnabled && !string.IsNullOrWhiteSpace(cart.Email))
            {
                var subject = AbandonedCartMessageTemplate.Render(settings.AbandonedCartEmailSubject, values);
                var body = AbandonedCartMessageTemplate.RenderHtml(settings.AbandonedCartEmailTemplate, values);

                if (!string.IsNullOrWhiteSpace(subject) && !string.IsNullOrWhiteSpace(body))
                {
                    try
                    {
                        await _emailSender.SendAsync(cart.Email, subject, body, cancellationToken);
                        channels.Add("email");
                    }
                    catch (Exception exception)
                    {
                        _logger.LogError(
                            exception,
                            "Failed to send abandoned-cart email for cart {CartId}.",
                            cart.Id);
                    }
                }
            }

            if (channels.Count == 0)
            {
                // Nothing was delivered: drop the unused code so it cannot be
                // redeemed, and let the next tick retry this cart.
                _dbContext.Discounts.Remove(discount);
                continue;
            }

            cart.MarkReminded(code, string.Join(",", channels), now);
            sentCount++;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return sentCount;
    }

    private async Task<List<PaidOrderInfo>> LoadPaidOrdersAsync(
        IReadOnlyCollection<AbandonedCart> carts,
        CancellationToken cancellationToken)
    {
        var phoneNumbers = carts
            .Where(cart => cart.PhoneNumber != null)
            .Select(cart => cart.PhoneNumber!)
            .Distinct()
            .ToList();

        var customerIds = carts
            .Where(cart => cart.CustomerId != null)
            .Select(cart => cart.CustomerId!.Value)
            .Distinct()
            .ToList();

        if (phoneNumbers.Count == 0 && customerIds.Count == 0)
        {
            return [];
        }

        return await _dbContext.Orders
            .AsNoTracking()
            .Where(order => order.Status == OrderStatus.Paid && order.PaidAt != null)
            .Where(order =>
                (order.CustomerId != null && customerIds.Contains(order.CustomerId.Value)) ||
                phoneNumbers.Contains(order.CustomerPhone))
            .Select(order => new PaidOrderInfo(
                order.CustomerId,
                order.CustomerPhone,
                order.PaidAt!.Value))
            .ToListAsync(cancellationToken);
    }

    private static bool IsConverted(AbandonedCart cart, IReadOnlyCollection<PaidOrderInfo> paidOrders)
        => paidOrders.Any(order =>
            order.PaidAt >= cart.LastActivityAt &&
            ((cart.CustomerId is not null && order.CustomerId == cart.CustomerId) ||
             (cart.PhoneNumber is not null && order.CustomerPhone == cart.PhoneNumber)));

    private async Task<string> GenerateUniqueCodeAsync(
        IReadOnlySet<string> reservedCodes,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var candidate = BuildCandidateCode();

            if (reservedCodes.Contains(candidate))
            {
                continue;
            }

            var exists = await _dbContext.Discounts
                .AnyAsync(discount => discount.Code == candidate, cancellationToken);

            if (!exists)
            {
                return candidate;
            }
        }

        // Extremely unlikely; a longer GUID-derived code is still unique enough
        // and stays below the 64-character discount code limit.
        return $"{CodePrefix}-{Guid.NewGuid():N}".ToUpperInvariant();
    }

    private static string BuildCandidateCode()
        => $"{CodePrefix}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";

    private static Discount CreateReminderDiscount(
        string code,
        decimal percent,
        int validityHours,
        DateTimeOffset now)
        => new(
            name: "کد سبد رهاشده",
            description: "کد یک‌بارمصرف بازگشت سبد خرید (ارسال خودکار)",
            code: code,
            type: DiscountType.AmountOff,
            valueKind: DiscountValueKind.Percentage,
            value: percent,
            maxDiscountAmount: null,
            minCartAmount: 0m,
            startsAt: now,
            endsAt: now.AddHours(validityHours),
            isActive: true,
            priority: 0,
            isStackable: false,
            maxTotalUses: 1,
            maxUsesPerCustomer: 1,
            appliesTo: DiscountAppliesTo.Order,
            customerGroup: DiscountCustomerGroup.All);

    private static IReadOnlyDictionary<string, string> BuildTemplateValues(
        AbandonedCart cart,
        Sugarista.Domain.Entities.SiteSettings settings,
        string code,
        decimal percent,
        int validityHours)
    {
        var name = string.IsNullOrWhiteSpace(cart.CustomerName) ? "دوست عزیز" : cart.CustomerName;

        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["name"] = name,
            ["cafe"] = string.IsNullOrWhiteSpace(settings.CafeName) ? "کافه" : settings.CafeName,
            ["code"] = code,
            ["percent"] = percent.ToString("0.##"),
            ["validityHours"] = validityHours.ToString(),
            ["itemCount"] = cart.ItemCount.ToString(),
            ["itemsTotal"] = cart.ItemsTotal.ToString("N0"),
            ["items"] = BuildItemsSummary(cart.ItemsJson)
        };
    }

    private static string BuildItemsSummary(string itemsJson)
    {
        try
        {
            var items = JsonSerializer.Deserialize<List<CartItemSnapshot>>(itemsJson, SnapshotOptions) ?? [];

            return string.Join("\n", items.Select(item => $"- {item.PersianName} × {item.Quantity}"));
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    private sealed record CartItemSnapshot(string? PersianName, int Quantity);

    private sealed record PaidOrderInfo(Guid? CustomerId, string? CustomerPhone, DateTimeOffset PaidAt);
}
