using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

public enum CustomerSubscriptionStatus
{
    Active = 0,
    Cancelled = 1,
    Expired = 2
}

/// <summary>
/// A customer's active subscription. Values are snapshotted from the package
/// at activation time so later package edits never change a sold contract.
/// Each use consumes one eligible product unit; the daily counter resets on
/// the first use of a new day.
/// </summary>
public class CustomerSubscription : BaseAuditableEntity
{
    public CustomerSubscription(
        Guid customerId,
        Guid packageId,
        string packageName,
        int totalUseCount,
        decimal price,
        int validityDays,
        int dailyUseLimit,
        string? allowedCategoryIds)
    {
        if (customerId == Guid.Empty || packageId == Guid.Empty)
        {
            throw new DomainException("Subscription needs a customer and a package.");
        }

        if (totalUseCount <= 0 || validityDays <= 0)
        {
            throw new DomainException("Subscription use count and validity must be positive.");
        }

        CustomerId = customerId;
        PackageId = packageId;
        PackageName = packageName;
        TotalUseCount = totalUseCount;
        UsedCount = 0;
        Price = price;
        DailyUseLimit = dailyUseLimit;
        AllowedCategoryIds = string.IsNullOrWhiteSpace(allowedCategoryIds) ? null : allowedCategoryIds;
        StartsAt = DateTimeOffset.UtcNow;
        ExpiresAt = StartsAt.AddDays(validityDays);
        Status = CustomerSubscriptionStatus.Active;
        UsesToday = 0;
    }

    private CustomerSubscription()
    {
    }

    public Guid CustomerId { get; private set; }

    public Customer Customer { get; private set; } = null!;

    public Guid PackageId { get; private set; }

    public SubscriptionPackage Package { get; private set; } = null!;

    public string PackageName { get; private set; } = string.Empty;

    public int TotalUseCount { get; private set; }

    public int UsedCount { get; private set; }

    public decimal Price { get; private set; }

    public int DailyUseLimit { get; private set; }

    public string? AllowedCategoryIds { get; private set; }

    public int UsesToday { get; private set; }

    public DateOnly? LastUseDate { get; private set; }

    public DateTimeOffset StartsAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public CustomerSubscriptionStatus Status { get; private set; }

    public decimal PricePerUse => TotalUseCount > 0 ? Math.Round(Price / TotalUseCount, 2) : 0m;

    public int RemainingUses => Math.Max(0, TotalUseCount - UsedCount);

    public decimal RemainingValue => Math.Round(RemainingUses * PricePerUse, 2);

    public bool IsUsable(DateTimeOffset now) =>
        Status == CustomerSubscriptionStatus.Active && ExpiresAt > now && RemainingUses > 0;

    /// <summary>Remaining uses available today (after applying the daily cap).</summary>
    public int AvailableToday(DateOnly today)
    {
        var usedToday = LastUseDate == today ? UsesToday : 0;
        var cap = DailyUseLimit > 0 ? DailyUseLimit : int.MaxValue;
        return Math.Max(0, Math.Min(RemainingUses, cap - usedToday));
    }

    /// <summary>Consumes uses; refuses to go beyond the granted count or the daily cap.</summary>
    public void Consume(int count, DateOnly today)
    {
        if (count <= 0)
        {
            return;
        }

        if (!IsUsable(DateTimeOffset.UtcNow))
        {
            throw new DomainException("اشتراک فعال یا معتبر نیست.");
        }

        if (count > RemainingUses)
        {
            throw new DomainException("تعداد مصرف از باقی‌ماندهٔ اشتراک بیشتر است.");
        }

        if (count > AvailableToday(today))
        {
            throw new DomainException("سقف مصرف روزانهٔ اشتراک رعایت نشده است.");
        }

        if (LastUseDate == today)
        {
            UsesToday += count;
        }
        else
        {
            LastUseDate = today;
            UsesToday = count;
        }

        UsedCount += count;
    }

    public void MarkExpired()
    {
        if (Status == CustomerSubscriptionStatus.Active)
        {
            Status = CustomerSubscriptionStatus.Expired;
        }
    }

    public void Cancel()
    {
        if (Status == CustomerSubscriptionStatus.Cancelled)
        {
            return;
        }

        Status = CustomerSubscriptionStatus.Cancelled;
    }
}
