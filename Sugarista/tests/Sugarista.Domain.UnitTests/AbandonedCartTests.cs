using Sugarista.Domain.Entities;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.UnitTests;

public class AbandonedCartTests
{
    private static AbandonedCart CreateCart(string token = "cart-token") => new(
        token,
        "  سارا  ",
        " 0912 345 6789 ",
        "SARA@Example.com ",
        null,
        "[{\"persianName\":\"لاته\",\"quantity\":2}]",
        250_000m,
        2,
        new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Constructor_ShouldNormalizeFieldsAndStartActive()
    {
        var cart = CreateCart();

        Assert.Equal("cart-token", cart.CartToken);
        Assert.Equal("سارا", cart.CustomerName);
        Assert.Equal("09123456789", cart.PhoneNumber);
        Assert.Equal("sara@example.com", cart.Email);
        Assert.Equal(250_000m, cart.ItemsTotal);
        Assert.Equal(2, cart.ItemCount);
        Assert.Equal(AbandonedCartStatus.Active, cart.Status);
        Assert.Null(cart.ReminderCode);
        Assert.Null(cart.ReminderSentAt);
    }

    [Fact]
    public void Constructor_ShouldRejectEmptyToken()
    {
        Assert.Throws<DomainException>(() => new AbandonedCart(
            "   ",
            null,
            null,
            null,
            null,
            "[]",
            0m,
            0,
            DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Constructor_ShouldClampNegativeTotalsAndFallbackToEmptyJson()
    {
        var cart = new AbandonedCart(
            "token",
            null,
            null,
            null,
            null,
            "  ",
            -10m,
            -3,
            DateTimeOffset.UtcNow);

        Assert.Equal("[]", cart.ItemsJson);
        Assert.Equal(0m, cart.ItemsTotal);
        Assert.Equal(0, cart.ItemCount);
    }

    [Fact]
    public void Touch_ShouldRefreshSnapshotAndKeepKnownContact()
    {
        var cart = CreateCart();
        var activity = new DateTimeOffset(2026, 1, 2, 8, 30, 0, TimeSpan.Zero);

        cart.Touch(
            null,
            null,
            null,
            null,
            "[{\"persianName\":\"چیزکیک\",\"quantity\":1}]",
            120_000m,
            1,
            activity);

        Assert.Equal("سارا", cart.CustomerName);
        Assert.Equal("09123456789", cart.PhoneNumber);
        Assert.Equal("sara@example.com", cart.Email);
        Assert.Equal(120_000m, cart.ItemsTotal);
        Assert.Equal(1, cart.ItemCount);
        Assert.Equal(activity, cart.LastActivityAt);
        Assert.Equal(AbandonedCartStatus.Active, cart.Status);
    }

    [Fact]
    public void Touch_ShouldKeepRemindedStateAndCode()
    {
        var cart = CreateCart();
        var sentAt = DateTimeOffset.UtcNow;
        cart.MarkReminded("cart-abc123", "sms", sentAt);

        cart.Touch(null, null, null, null, "[]", 0m, 0, DateTimeOffset.UtcNow.AddMinutes(5));

        Assert.Equal(AbandonedCartStatus.Reminded, cart.Status);
        Assert.Equal("CART-ABC123", cart.ReminderCode);
        Assert.Equal(sentAt, cart.ReminderSentAt);
    }

    [Fact]
    public void Touch_ShouldRestartConvertedCartAndClearReminder()
    {
        var cart = CreateCart();
        cart.MarkReminded("CART-ABC123", "sms", DateTimeOffset.UtcNow);
        cart.MarkConverted(DateTimeOffset.UtcNow);

        cart.Touch(null, null, null, null, "[]", 0m, 0, DateTimeOffset.UtcNow.AddHours(1));

        Assert.Equal(AbandonedCartStatus.Active, cart.Status);
        Assert.Null(cart.ReminderCode);
        Assert.Null(cart.ReminderSentAt);
        Assert.Null(cart.ReminderChannel);
        Assert.Null(cart.ConvertedAt);
    }

    [Fact]
    public void Touch_ShouldRestartExpiredCart()
    {
        var cart = CreateCart();
        cart.MarkExpired();

        cart.Touch(null, null, null, null, "[]", 0m, 0, DateTimeOffset.UtcNow);

        Assert.Equal(AbandonedCartStatus.Active, cart.Status);
    }

    [Fact]
    public void MarkReminded_ShouldStoreUppercaseCodeAndChannel()
    {
        var cart = CreateCart();
        var sentAt = DateTimeOffset.UtcNow;

        cart.MarkReminded(" cart-ab12cd ", " sms,email ", sentAt);

        Assert.Equal("CART-AB12CD", cart.ReminderCode);
        Assert.Equal("sms,email", cart.ReminderChannel);
        Assert.Equal(sentAt, cart.ReminderSentAt);
        Assert.Equal(AbandonedCartStatus.Reminded, cart.Status);
    }

    [Fact]
    public void MarkReminded_ShouldRejectEmptyCode()
    {
        var cart = CreateCart();

        Assert.Throws<DomainException>(() => cart.MarkReminded("   ", "sms", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void MarkConverted_ShouldRecordTimestamp()
    {
        var cart = CreateCart();
        var convertedAt = DateTimeOffset.UtcNow;

        cart.MarkConverted(convertedAt);

        Assert.Equal(AbandonedCartStatus.Converted, cart.Status);
        Assert.Equal(convertedAt, cart.ConvertedAt);
    }

    [Fact]
    public void MarkExpired_ShouldSetExpiredStatus()
    {
        var cart = CreateCart();

        cart.MarkExpired();

        Assert.Equal(AbandonedCartStatus.Expired, cart.Status);
    }
}
