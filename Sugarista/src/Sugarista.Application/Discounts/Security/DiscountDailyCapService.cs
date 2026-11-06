using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;

namespace Sugarista.Application.Discounts.Security;

/// <summary>
/// Enforces the per-customer daily discount cap. Only paid / partially paid
/// orders consume the cap, so pending or held orders never lock a customer out
/// before they actually pay. The cap itself is a site setting, read on every
/// request — changing it in the panel applies immediately without a restart.
/// </summary>
public static class DiscountDailyCapService
{
    /// <summary>UTC range covering "today" in the cafe's local time zone.</summary>
    public static (DateTimeOffset Start, DateTimeOffset End) GetLocalDayRange(
        DateTimeOffset now,
        string? timeZoneId)
    {
        var timeZone = ResolveTimeZone(timeZoneId);
        var localNow = TimeZoneInfo.ConvertTime(now, timeZone);
        var startLocal = new DateTime(localNow.Year, localNow.Month, localNow.Day, 0, 0, 0, DateTimeKind.Unspecified);
        var startUtc = TimeZoneInfo.ConvertTimeToUtc(startLocal, timeZone);
        var endUtc = TimeZoneInfo.ConvertTimeToUtc(startLocal.AddDays(1), timeZone);
        return (new DateTimeOffset(startUtc, TimeSpan.Zero), new DateTimeOffset(endUtc, TimeSpan.Zero));
    }

    /// <summary>
    /// Remaining discount allowance for the customer today. <c>null</c> means the
    /// cap is disabled/unlimited. Never negative.
    /// </summary>
    public static async Task<decimal?> GetRemainingAllowanceAsync(
        IApplicationDbContext dbContext,
        Sugarista.Domain.Entities.SiteSettings? settings,
        Guid? customerId,
        string? timeZoneId,
        DateTimeOffset now,
        Guid? excludeOrderId = null,
        CancellationToken cancellationToken = default)
    {
        if (settings is null ||
            !settings.DiscountAbuseProtectionEnabled ||
            settings.DiscountDailyCapPerCustomer <= 0 ||
            customerId is null)
        {
            return null;
        }

        var (start, end) = GetLocalDayRange(now, timeZoneId);

        var used = await dbContext.OrderDiscounts
            .AsNoTracking()
            .Where(discount => discount.CustomerId == customerId &&
                               discount.RedeemedAt >= start &&
                               discount.RedeemedAt < end &&
                               (excludeOrderId == null || discount.OrderId != excludeOrderId))
            .Join(
                dbContext.Orders.AsNoTracking(),
                discount => discount.OrderId,
                order => order.Id,
                (discount, order) => new { discount.Amount, order.Status })
            .Where(entry => entry.Status == Sugarista.Domain.Entities.OrderStatus.Paid ||
                            entry.Status == Sugarista.Domain.Entities.OrderStatus.PartiallyPaid)
            .SumAsync(entry => (decimal?)entry.Amount, cancellationToken) ?? 0m;

        return Math.Max(0m, settings.DiscountDailyCapPerCustomer - used);
    }

    private static TimeZoneInfo ResolveTimeZone(string? timeZoneId)
    {
        if (!string.IsNullOrWhiteSpace(timeZoneId))
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran");
    }
}
