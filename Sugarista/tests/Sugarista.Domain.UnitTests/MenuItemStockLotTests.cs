using Sugarista.Domain.Entities;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.UnitTests;

public class MenuItemStockLotTests
{
    [Fact]
    public void Constructor_InitializesQuantitiesAndNormalizesDatesAndLabel()
    {
        var menuItemId = Guid.NewGuid();
        var received = new DateTimeOffset(2026, 11, 25, 10, 0, 0, TimeSpan.FromHours(3.5));
        var expires = new DateTimeOffset(2026, 11, 27, 23, 59, 59, TimeSpan.FromHours(3.5));

        var lot = new MenuItemStockLot(menuItemId, 3m, received, expires, "  پارت صبح  ");

        Assert.Equal(menuItemId, lot.MenuItemId);
        Assert.Equal(3m, lot.InitialQuantity);
        Assert.Equal(3m, lot.RemainingQuantity);
        Assert.Equal(received.ToUniversalTime(), lot.ReceivedAt);
        Assert.Equal(expires.ToUniversalTime(), lot.ExpiresAt);
        Assert.Equal("پارت صبح", lot.BatchLabel);
        Assert.True(lot.IsActive);
        Assert.False(lot.IsDiscarded);
    }

    [Fact]
    public void Constructor_RejectsEmptyMenuItemOrNegativeQuantity()
    {
        var now = DateTimeOffset.UtcNow;
        Assert.Throws<DomainException>(() => new MenuItemStockLot(Guid.Empty, 1m, now));
        Assert.Throws<DomainException>(() => new MenuItemStockLot(Guid.NewGuid(), -1m, now));
    }

    [Fact]
    public void Consume_CapsAtRemainingQuantity_AndNeverDropsBelowZero()
    {
        var lot = new MenuItemStockLot(Guid.NewGuid(), 2m, DateTimeOffset.UtcNow);

        var firstTaken = lot.Consume(1m);
        Assert.Equal(1m, firstTaken);
        Assert.Equal(1m, lot.RemainingQuantity);
        Assert.True(lot.IsActive);

        var secondTaken = lot.Consume(5m);
        Assert.Equal(1m, secondTaken);
        Assert.Equal(0m, lot.RemainingQuantity);
        Assert.False(lot.IsActive);

        var thirdTaken = lot.Consume(1m);
        Assert.Equal(0m, thirdTaken);
        Assert.Equal(0m, lot.RemainingQuantity);
    }

    [Fact]
    public void Restore_IncreasesRemainingAndInitialQuantity()
    {
        var lot = new MenuItemStockLot(Guid.NewGuid(), 2m, DateTimeOffset.UtcNow);
        lot.Consume(2m);
        Assert.Equal(0m, lot.RemainingQuantity);

        lot.Restore(3m);
        Assert.Equal(3m, lot.RemainingQuantity);
        Assert.Equal(3m, lot.InitialQuantity);
        Assert.True(lot.IsActive);
    }

    [Fact]
    public void Discard_SetsRemainingToZeroAndMarksDiscarded()
    {
        var lot = new MenuItemStockLot(Guid.NewGuid(), 4m, DateTimeOffset.UtcNow);

        lot.Discard("منقضی شده");

        Assert.True(lot.IsDiscarded);
        Assert.False(lot.IsActive);
        Assert.Equal(0m, lot.RemainingQuantity);
        Assert.Equal("منقضی شده", lot.DiscardReason);
        Assert.NotNull(lot.DiscardedAt);
        Assert.Equal(0m, lot.Consume(1m));
    }

    [Fact]
    public void UpdateDetails_UpdatesFieldsAndRevivesDiscardedLotWhenQuantityPositive()
    {
        var lot = new MenuItemStockLot(Guid.NewGuid(), 2m, DateTimeOffset.UtcNow);
        lot.Discard("تست");

        var newReceived = DateTimeOffset.UtcNow.AddHours(-2);
        var newExpires = DateTimeOffset.UtcNow.AddDays(2);
        lot.UpdateDetails(5m, newReceived, newExpires, "تازه");

        Assert.False(lot.IsDiscarded);
        Assert.Null(lot.DiscardedAt);
        Assert.Null(lot.DiscardReason);
        Assert.Equal(5m, lot.RemainingQuantity);
        Assert.Equal(5m, lot.InitialQuantity);
        Assert.Equal("تازه", lot.BatchLabel);
    }

    [Fact]
    public void MenuItem_ApplyQuickPricingAndAvailability_UpdatesAllPricingFieldsAndProtectsUnpricedOrderable()
    {
        var item = new MenuItem();
        item.ApplyQuickPricingAndAvailability(
            price: 185000m,
            specialOfferPrice: 165000m,
            purchasePrice: 92000m,
            smallPrice: 140000m,
            smallSpecialOfferPrice: 130000m,
            mediumPrice: 185000m,
            mediumSpecialOfferPrice: 165000m,
            largePrice: 220000m,
            largeSpecialOfferPrice: null,
            isActive: true,
            isOrderable: true);

        Assert.Equal(185000m, item.Price);
        Assert.Equal(165000m, item.SpecialOfferPrice);
        Assert.Equal(92000m, item.PurchasePrice);
        Assert.Equal(140000m, item.SmallPrice);
        Assert.Equal(130000m, item.SmallSpecialOfferPrice);
        Assert.Equal(185000m, item.MediumPrice);
        Assert.Equal(220000m, item.LargePrice);
        Assert.True(item.IsActive);
        Assert.True(item.IsOrderable);

        // When all prices are cleared, IsOrderable must automatically become false
        item.ApplyQuickPricingAndAvailability(
            price: null,
            specialOfferPrice: null,
            purchasePrice: null,
            smallPrice: null,
            smallSpecialOfferPrice: null,
            mediumPrice: null,
            mediumSpecialOfferPrice: null,
            largePrice: null,
            largeSpecialOfferPrice: null,
            isActive: true,
            isOrderable: true);

        Assert.Equal(0m, item.Price);
        Assert.False(item.IsOrderable);
    }

    [Fact]
    public void MenuItem_ApplyQuickPricingAndAvailability_RejectsNegativePurchasePrice()
    {
        var item = new MenuItem();
        Assert.Throws<DomainException>(() => item.ApplyQuickPricingAndAvailability(
            price: 100000m,
            specialOfferPrice: null,
            purchasePrice: -500m,
            smallPrice: null,
            smallSpecialOfferPrice: null,
            mediumPrice: null,
            mediumSpecialOfferPrice: null,
            largePrice: null,
            largeSpecialOfferPrice: null,
            isActive: true,
            isOrderable: true));
    }
}
