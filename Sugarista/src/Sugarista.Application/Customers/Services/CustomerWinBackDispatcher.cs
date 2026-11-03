using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Customers.Services;

/// <summary>
/// Sends the automatic win-back discount SMS to customers that stayed without
/// a single paid order for longer than the configured <c>WinBackDays</c> window.
/// Each customer receives at most one win-back code per inactivity window.
/// </summary>
public class CustomerWinBackDispatcher
{
    public const string WinBackDiscountCode = "WINBACK";

    private readonly IApplicationDbContext _dbContext;
    private readonly IMaintenanceSmsSender _smsSender;
    private readonly ILogger<CustomerWinBackDispatcher> _logger;

    public CustomerWinBackDispatcher(
        IApplicationDbContext dbContext,
        IMaintenanceSmsSender smsSender,
        ILogger<CustomerWinBackDispatcher> logger)
    {
        _dbContext = dbContext;
        _smsSender = smsSender;
        _logger = logger;
    }

    /// <summary>
    /// Sends one win-back code per due customer (no paid order inside the
    /// window and no win-back sent inside the same window). Returns the number
    /// of win-back SMS sent in this run.
    /// </summary>
    public async Task<int> DispatchDueWinBacksAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _dbContext.SiteSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        var windowDays = settings?.WinBackDays ?? 30;
        if (windowDays <= 0)
        {
            // Admin disabled the win-back sweep entirely.
            return 0;
        }

        var cutoff = DateTimeOffset.UtcNow.AddDays(-windowDays);

        var dueCustomers = await _dbContext.Customers
            .Where(customer => customer.IsActive)
            .Where(customer => customer.WinBackSentAt == null || customer.WinBackSentAt < cutoff)
            .Where(customer => !customer.Orders.Any(order =>
                order.Status == OrderStatus.Paid && order.PaidAt != null && order.PaidAt.Value >= cutoff))
            .OrderBy(customer => customer.CreatedAt)
            .Take(50)
            .ToListAsync(cancellationToken);

        if (dueCustomers.Count == 0)
        {
            return 0;
        }

        var winBackDiscount = await _dbContext.Discounts
            .AsNoTracking()
            .FirstOrDefaultAsync(discount => discount.Code == WinBackDiscountCode, cancellationToken);

        if (winBackDiscount is null)
        {
            // Lazily create the shared win-back discount so the SMS code always resolves.
            _dbContext.Discounts.Add(new Discount(
                "کد بازگشت مشتری",
                "کد تخفیف بازگشت برای مشتریان باقیمانده",
                WinBackDiscountCode,
                DiscountType.AmountOff,
                DiscountValueKind.Percentage,
                10m,
                null,
                0m,
                null,
                null,
                true,
                0,
                isStackable: false,
                maxTotalUses: null,
                maxUsesPerCustomer: 1,
                appliesTo: DiscountAppliesTo.Order,
                customerGroup: DiscountCustomerGroup.All));

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var sentCount = 0;

        foreach (var customer in dueCustomers)
        {
            if (customer.WinBackSentAt is { } alreadySent && alreadySent >= cutoff)
            {
                continue;
            }

            var message = $"سلام {customer.PersianName}, به شوگاریستا خوش اومدید. برای بازگشت به این رستوران، کد تخفیف «{WinBackDiscountCode}» را در سبد خرید وارد کنید.";

            try
            {
                await _smsSender.SendAsync(customer.PhoneNumber, message, cancellationToken);
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Failed to send win-back SMS to customer {CustomerId} ({PhoneNumber}).",
                    customer.Id,
                    customer.PhoneNumber);
                continue;
            }

            customer.MarkWinBackSent(DateTimeOffset.UtcNow);
            sentCount++;
        }

        if (sentCount > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return sentCount;
    }
}
