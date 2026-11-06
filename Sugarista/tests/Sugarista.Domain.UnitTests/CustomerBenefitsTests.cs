using Sugarista.Domain.Entities;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.UnitTests;

public class CustomerSubscriptionTests
{
    private static CustomerSubscription CreateSubscription(
        int totalUseCount = 10,
        decimal price = 500_000m,
        int validityDays = 30,
        int dailyUseLimit = 0) =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "اشتراک تست",
            totalUseCount,
            price,
            validityDays,
            dailyUseLimit,
            allowedCategoryIds: null);

    [Fact]
    public void RemainingValue_ShouldReflectUsedUses()
    {
        var subscription = CreateSubscription(totalUseCount: 10, price: 500_000m);

        subscription.Consume(3, DateOnly.FromDateTime(DateTime.UtcNow));

        Assert.Equal(7, subscription.RemainingUses);
        Assert.Equal(350_000m, subscription.RemainingValue);
    }

    [Fact]
    public void Consume_ShouldTrackDailyUsage()
    {
        var subscription = CreateSubscription(dailyUseLimit: 2);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        subscription.Consume(2, today);

        Assert.Equal(2, subscription.UsesToday);
        Assert.Equal(today, subscription.LastUseDate);
        Assert.Equal(0, subscription.AvailableToday(today));
    }

    [Fact]
    public void Consume_ShouldThrow_WhenDailyLimitExceeded()
    {
        var subscription = CreateSubscription(dailyUseLimit: 2);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        subscription.Consume(2, today);

        Assert.Throws<DomainException>(() => subscription.Consume(1, today));
    }

    [Fact]
    public void Consume_ShouldThrow_WhenRemainingExceeded()
    {
        var subscription = CreateSubscription(totalUseCount: 2);

        Assert.Throws<DomainException>(() => subscription.Consume(3, DateOnly.FromDateTime(DateTime.UtcNow)));
    }

    [Fact]
    public void AvailableToday_ShouldResetOnANewDay()
    {
        var subscription = CreateSubscription(dailyUseLimit: 2);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        subscription.Consume(2, today);

        Assert.Equal(2, subscription.AvailableToday(today.AddDays(1)));
    }

    [Fact]
    public void Cancel_ShouldStopUsability()
    {
        var subscription = CreateSubscription();

        subscription.Cancel();

        Assert.Equal(CustomerSubscriptionStatus.Cancelled, subscription.Status);
        Assert.False(subscription.IsUsable(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void MarkExpired_ShouldOnlyAffectActiveSubscriptions()
    {
        var subscription = CreateSubscription();
        subscription.Cancel();

        subscription.MarkExpired();

        Assert.Equal(CustomerSubscriptionStatus.Cancelled, subscription.Status);
    }
}

public class CustomerGroupMembershipTests
{
    [Fact]
    public void Membership_WithoutValidity_ShouldNeverExpire()
    {
        var membership = new CustomerGroupMembership(
            Guid.NewGuid(), Guid.NewGuid(), CustomerGroupMembershipSource.Manual, validityDays: 0);

        Assert.Null(membership.ExpiresAt);
        Assert.True(membership.IsActive(DateTimeOffset.UtcNow.AddYears(50)));
    }

    [Fact]
    public void Membership_WithValidity_ShouldExpireAfterPeriod()
    {
        var membership = new CustomerGroupMembership(
            Guid.NewGuid(), Guid.NewGuid(), CustomerGroupMembershipSource.EmailDomain, validityDays: 30);

        Assert.True(membership.IsActive(DateTimeOffset.UtcNow.AddDays(29)));
        Assert.False(membership.IsActive(DateTimeOffset.UtcNow.AddDays(31)));
    }

    [Fact]
    public void Renew_ShouldRestartTheValidityWindow()
    {
        var membership = new CustomerGroupMembership(
            Guid.NewGuid(), Guid.NewGuid(), CustomerGroupMembershipSource.Manual, validityDays: 10);

        membership.Renew(validityDays: 60);

        Assert.True(membership.IsActive(DateTimeOffset.UtcNow.AddDays(59)));
        Assert.False(membership.IsActive(DateTimeOffset.UtcNow.AddDays(61)));
    }
}
