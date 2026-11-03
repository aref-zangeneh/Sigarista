using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

/// <summary>
/// A single discount rule. Everything that can reduce an order total — promo
/// codes, product/category offers, happy hour, tiered carts, loyalty rewards —
/// is stored as a row here and evaluated by the central discount engine.
/// </summary>
public class Discount : BaseAuditableEntity
{
    private Discount()
    {
    }

    public Discount(
        string name,
        string? description,
        string? code,
        DiscountType type,
        DiscountValueKind valueKind,
        decimal value,
        decimal? maxDiscountAmount,
        decimal minCartAmount,
        DateTimeOffset? startsAt,
        DateTimeOffset? endsAt,
        bool isActive,
        int priority,
        bool isStackable,
        int? maxTotalUses,
        int? maxUsesPerCustomer,
        DiscountAppliesTo appliesTo,
        DiscountCustomerGroup customerGroup,
        int buyQuantity = 1,
        int getQuantity = 1,
        FreeItemSelection freeItemSelection = FreeItemSelection.Cheapest,
        int? maxApplications = null,
        bool isFlashSale = false)
    {
        Update(
            name,
            description,
            code,
            type,
            valueKind,
            value,
            maxDiscountAmount,
            minCartAmount,
            startsAt,
            endsAt,
            isActive,
            priority,
            isStackable,
            maxTotalUses,
            maxUsesPerCustomer,
            appliesTo,
            customerGroup,
            buyQuantity,
            getQuantity,
            freeItemSelection,
            maxApplications,
            isFlashSale);
    }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    /// <summary>
    /// Promo code customers type at checkout. Empty means the discount is
    /// applied automatically whenever its conditions match.
    /// </summary>
    public string? Code { get; private set; }

    public DiscountType Type { get; private set; } = DiscountType.AmountOff;

    public DiscountValueKind ValueKind { get; private set; } = DiscountValueKind.Percentage;

    /// <summary>Percentage (0-100) or a fixed amount, per <see cref="ValueKind"/>.</summary>
    public decimal Value { get; private set; }

    /// <summary>Upper bound for the money a percentage discount can take off.</summary>
    public decimal? MaxDiscountAmount { get; private set; }

    /// <summary>The cart subtotal (items only) required before the discount applies.</summary>
    public decimal MinCartAmount { get; private set; }

    public DateTimeOffset? StartsAt { get; private set; }

    public DateTimeOffset? EndsAt { get; private set; }

    public bool IsActive { get; private set; } = true;

    /// <summary>Higher priority wins when several discounts compete; lower runs first in a stack.</summary>
    public int Priority { get; private set; }

    /// <summary>When false the discount cannot be combined with any other discount.</summary>
    public bool IsStackable { get; private set; } = true;

    public int? MaxTotalUses { get; private set; }

    public int? MaxUsesPerCustomer { get; private set; }

    public DiscountAppliesTo AppliesTo { get; private set; } = DiscountAppliesTo.Order;

    public DiscountCustomerGroup CustomerGroup { get; private set; } = DiscountCustomerGroup.All;

    public ICollection<DiscountMenuItem> MenuItems { get; private set; } = [];

    public ICollection<DiscountCategory> Categories { get; private set; } = [];

    public ICollection<DiscountSchedule> Schedules { get; private set; } = [];

    /// <summary>
    /// Spending ladder for <see cref="DiscountType.TieredCart"/> discounts. The
    /// highest tier whose threshold the cart reaches is the one that applies.
    /// </summary>
    public ICollection<DiscountTier> Tiers { get; private set; } = [];

    /// <summary>Buy-X-Get-Y: how many eligible items must be bought.</summary>
    public int BuyQuantity { get; private set; } = 1;

    /// <summary>Buy-X-Get-Y: how many eligible items are given away for free.</summary>
    public int GetQuantity { get; private set; } = 1;

    /// <summary>Buy-X-Get-Y: which eligible items are given away.</summary>
    public FreeItemSelection FreeItemSelection { get; private set; } = FreeItemSelection.Cheapest;

    /// <summary>Buy-X-Get-Y: cap on how many times the offer applies (null = unlimited).</summary>
    public int? MaxApplications { get; private set; }

    /// <summary>Whether this discount is advertised as a flash sale (homepage banner + countdown).</summary>
    public bool IsFlashSale { get; private set; }

    public bool IsAutomatic => string.IsNullOrWhiteSpace(Code);

    public void Update(
        string name,
        string? description,
        string? code,
        DiscountType type,
        DiscountValueKind valueKind,
        decimal value,
        decimal? maxDiscountAmount,
        decimal minCartAmount,
        DateTimeOffset? startsAt,
        DateTimeOffset? endsAt,
        bool isActive,
        int priority,
        bool isStackable,
        int? maxTotalUses,
        int? maxUsesPerCustomer,
        DiscountAppliesTo appliesTo,
        DiscountCustomerGroup customerGroup,
        int buyQuantity = 1,
        int getQuantity = 1,
        FreeItemSelection freeItemSelection = FreeItemSelection.Cheapest,
        int? maxApplications = null,
        bool isFlashSale = false)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Discount name is required.");
        }

        if (value < 0)
        {
            throw new DomainException("Discount value cannot be negative.");
        }

        if (valueKind == DiscountValueKind.Percentage && value > 100)
        {
            throw new DomainException("A percentage discount cannot exceed 100.");
        }

        if (maxDiscountAmount is < 0)
        {
            throw new DomainException("The discount cap cannot be negative.");
        }

        if (minCartAmount < 0)
        {
            throw new DomainException("The minimum cart amount cannot be negative.");
        }

        if (startsAt.HasValue && endsAt.HasValue && endsAt < startsAt)
        {
            throw new DomainException("The discount end date cannot be before its start date.");
        }

        if (priority < 0)
        {
            throw new DomainException("Discount priority cannot be negative.");
        }

        if (maxTotalUses is <= 0)
        {
            throw new DomainException("The total usage limit must be greater than zero.");
        }

        if (maxUsesPerCustomer is <= 0)
        {
            throw new DomainException("The per-customer usage limit must be greater than zero.");
        }

        if (buyQuantity < 1)
        {
            throw new DomainException("Buy quantity must be at least one.");
        }

        if (getQuantity < 1)
        {
            throw new DomainException("Get quantity must be at least one.");
        }

        if (maxApplications is <= 0)
        {
            throw new DomainException("The application cap must be greater than zero.");
        }

        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        Code = NormalizeCode(code);
        Type = type;
        ValueKind = valueKind;
        Value = value;
        MaxDiscountAmount = maxDiscountAmount;
        MinCartAmount = minCartAmount;
        StartsAt = startsAt;
        EndsAt = endsAt;
        IsActive = isActive;
        Priority = priority;
        IsStackable = isStackable;
        MaxTotalUses = maxTotalUses;
        MaxUsesPerCustomer = maxUsesPerCustomer;
        AppliesTo = appliesTo;
        CustomerGroup = customerGroup;
        BuyQuantity = buyQuantity;
        GetQuantity = getQuantity;
        FreeItemSelection = freeItemSelection;
        MaxApplications = maxApplications;
        IsFlashSale = isFlashSale;
    }

    /// <summary>Replaces the item/category scope in one call (the admin form edits both together).</summary>
    public void ReplaceScope(
        IEnumerable<Guid> menuItemIds,
        IEnumerable<Guid> categoryIds,
        IEnumerable<DiscountScheduleInput> schedules)
    {
        MenuItems.Clear();
        foreach (var menuItemId in menuItemIds.Distinct())
        {
            MenuItems.Add(new DiscountMenuItem(Id, menuItemId));
        }

        Categories.Clear();
        foreach (var categoryId in categoryIds.Distinct())
        {
            Categories.Add(new DiscountCategory(Id, categoryId));
        }

        Schedules.Clear();
        foreach (var schedule in schedules)
        {
            Schedules.Add(new DiscountSchedule(Id, schedule.DayOfWeek, schedule.StartTime, schedule.EndTime));
        }
    }

    public void SetActive(bool isActive) => IsActive = isActive;

    /// <summary>Replaces the whole tier ladder in one call (the admin form edits all tiers together).</summary>
    public void ReplaceTiers(IEnumerable<DiscountTierInput> tiers)
    {
        Tiers.Clear();

        var ordered = tiers
            .OrderBy(tier => tier.ThresholdAmount)
            .ToList();

        if (ordered.Select(tier => tier.ThresholdAmount).Distinct().Count() != ordered.Count)
        {
            throw new DomainException("Each discount tier must have a unique threshold.");
        }

        foreach (var tier in ordered)
        {
            Tiers.Add(new DiscountTier(Id, tier.ThresholdAmount, tier.ValueKind, tier.Value));
        }
    }

    /// <summary>True when the discount is enabled and inside its date window.</summary>
    public bool IsLive(DateTimeOffset now)
    {
        if (!IsActive)
        {
            return false;
        }

        if (StartsAt.HasValue && now < StartsAt.Value)
        {
            return false;
        }

        if (EndsAt.HasValue && now > EndsAt.Value)
        {
            return false;
        }

        return true;
    }

    /// <summary>True when the discount has no time-of-week restriction or one matches.</summary>
    public bool MatchesSchedule(DateTimeOffset utcMoment, string? timeZoneId = null)
    {
        if (Schedules.Count == 0)
        {
            return true;
        }

        var moment = utcMoment;
        if (!string.IsNullOrWhiteSpace(timeZoneId))
        {
            try
            {
                var tz = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
                moment = TimeZoneInfo.ConvertTime(utcMoment, tz);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return Schedules.Any(schedule => schedule.Matches(moment));
    }

    /// <summary>True when the discount targets the whole order or at least one item/category.</summary>
    public bool HasScopeEntries => MenuItems.Count > 0 || Categories.Count > 0;

    public static string? NormalizeCode(string? code)
        => string.IsNullOrWhiteSpace(code) ? null : code.Trim().ToUpperInvariant();
}

/// <summary>A time-of-week window (e.g. happy hour 15:00-17:00 on weekdays).</summary>
public sealed record DiscountScheduleInput(DayOfWeek? DayOfWeek, TimeOnly? StartTime, TimeOnly? EndTime);
