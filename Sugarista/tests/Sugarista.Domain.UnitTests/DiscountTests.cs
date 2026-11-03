using Sugarista.Domain.Common;
using Sugarista.Domain.Entities;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.UnitTests;

public class DiscountTests
{
    private static Discount CreateDiscount(
        string name = "تخفیف تست",
        string? code = null,
        DiscountType type = DiscountType.AmountOff,
        DiscountValueKind valueKind = DiscountValueKind.Percentage,
        decimal value = 10m,
        decimal? maxDiscountAmount = null,
        decimal minCartAmount = 0m,
        DateTimeOffset? startsAt = null,
        DateTimeOffset? endsAt = null,
        bool isActive = true,
        int priority = 0,
        bool isStackable = true,
        int? maxTotalUses = null,
        int? maxUsesPerCustomer = null,
        DiscountAppliesTo appliesTo = DiscountAppliesTo.Order,
        DiscountCustomerGroup customerGroup = DiscountCustomerGroup.All) =>
        new(
            name,
            "توضیحات تخفیف",
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
            customerGroup);

    [Fact]
    public void IsStackable_ShouldDefaultToTrue()
    {
        var discount = CreateDiscount();

        Assert.True(discount.IsStackable);
    }

    [Fact]
    public void Update_ShouldAllowDisablingStackingPerDiscount()
    {
        var discount = CreateDiscount(isStackable: false);

        discount.Update(
            "تخفیف تست",
            "توضیحات تخفیف",
            null,
            DiscountType.AmountOff,
            DiscountValueKind.Percentage,
            decimal.Zero,
            null,
            decimal.Zero,
            null,
            null,
            true,
            0,
            isStackable: true,
            null,
            null,
            DiscountAppliesTo.Order,
            DiscountCustomerGroup.All);

        Assert.True(discount.IsStackable);
    }

    [Fact]
    public void IsLive_ShouldReturnFalseWhenInactive()
    {
        var discount = CreateDiscount(isActive: false);

        Assert.False(discount.IsLive(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void IsLive_ShouldReturnFalseBeforeStartDate()
    {
        var discount = CreateDiscount(startsAt: DateTimeOffset.UtcNow.AddDays(1));

        Assert.False(discount.IsLive(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void IsLive_ShouldReturnFalseAfterEndDate()
    {
        var discount = CreateDiscount(endsAt: DateTimeOffset.UtcNow.AddDays(-1));

        Assert.False(discount.IsLive(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void IsLive_ShouldReturnTrueInsideDateWindow()
    {
        var now = DateTimeOffset.UtcNow;
        var discount = CreateDiscount(startsAt: now.AddHours(-1), endsAt: now.AddHours(1));

        Assert.True(discount.IsLive(now));
    }

    [Fact]
    public void ReplaceScope_ShouldReplacePreviousItemScope()
    {
        var discount = CreateDiscount();
        discount.ReplaceScope([Guid.NewGuid()], [Guid.NewGuid()], []);

        Assert.Single(discount.MenuItems);
        Assert.Single(discount.Categories);

        discount.ReplaceScope([], [], []);

        Assert.Empty(discount.MenuItems);
        Assert.Empty(discount.Categories);
    }

    [Fact]
    public void ReplaceScope_ShouldRejectScheduleWithoutEndTime()
    {
        var discount = CreateDiscount();
        var schedule = new DiscountScheduleInput(DayOfWeek.Friday, TimeOnly.FromTimeSpan(TimeSpan.FromHours(15)), null);

        Assert.Throws<DomainException>(() => discount.ReplaceScope([], [], [schedule]));
    }
}
