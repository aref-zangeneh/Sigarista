using Sugarista.Application.Inventory.Services;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.UnitTests.Inventory;

public class MenuItemStockFefoTests
{
    [Fact]
    public void OrderLotsFefo_TiramisuYesterdayVsToday_ConsumesYesterdayLotFirst()
    {
        var tiramisuId = Guid.NewGuid();
        var yesterday = new DateTimeOffset(2026, 11, 25, 9, 0, 0, TimeSpan.Zero);
        var today = new DateTimeOffset(2026, 11, 26, 9, 0, 0, TimeSpan.Zero);

        var todayLot = new MenuItemStockLot(
            tiramisuId,
            quantity: 1m,
            receivedAt: today,
            expiresAt: today.AddDays(3),
            batchLabel: "تیرامیسو امروز");

        var yesterdayLot = new MenuItemStockLot(
            tiramisuId,
            quantity: 1m,
            receivedAt: yesterday,
            expiresAt: yesterday.AddDays(3),
            batchLabel: "تیرامیسو دیروز");

        var ordered = MenuItemStockService.OrderLotsFefo([todayLot, yesterdayLot]);

        Assert.Equal(2, ordered.Count);
        Assert.Same(yesterdayLot, ordered[0]);
        Assert.Same(todayLot, ordered[1]);

        // Simulate selling 1 Tiramisu: must deduct from yesterday's lot first
        var remaining = 1m;
        foreach (var lot in ordered)
        {
            if (remaining <= 0) break;
            remaining -= lot.Consume(remaining);
        }

        Assert.Equal(0m, yesterdayLot.RemainingQuantity);
        Assert.Equal(1m, todayLot.RemainingQuantity);
    }

    [Fact]
    public void OrderLotsFefo_PlacesDatedLotsBeforeUndatedLots_AndBreaksTiesByReceivedAt()
    {
        var itemId = Guid.NewGuid();
        var baseTime = new DateTimeOffset(2026, 11, 26, 8, 0, 0, TimeSpan.Zero);

        var undatedOlder = new MenuItemStockLot(itemId, 2m, baseTime.AddDays(-2), expiresAt: null, batchLabel: "بدون انقضا قدیم");
        var undatedNewer = new MenuItemStockLot(itemId, 2m, baseTime, expiresAt: null, batchLabel: "بدون انقضا جدید");
        var sameExpiryLaterArrival = new MenuItemStockLot(itemId, 1m, baseTime.AddHours(3), expiresAt: baseTime.AddDays(2), batchLabel: "پارت ۲");
        var sameExpiryEarlierArrival = new MenuItemStockLot(itemId, 1m, baseTime.AddHours(1), expiresAt: baseTime.AddDays(2), batchLabel: "پارت ۱");
        var earliestExpiry = new MenuItemStockLot(itemId, 1m, baseTime.AddHours(5), expiresAt: baseTime.AddDays(1), batchLabel: "فوری");

        var ordered = MenuItemStockService.OrderLotsFefo([
            undatedNewer,
            sameExpiryLaterArrival,
            undatedOlder,
            earliestExpiry,
            sameExpiryEarlierArrival
        ]);

        Assert.Same(earliestExpiry, ordered[0]);
        Assert.Same(sameExpiryEarlierArrival, ordered[1]);
        Assert.Same(sameExpiryLaterArrival, ordered[2]);
        Assert.Same(undatedOlder, ordered[3]);
        Assert.Same(undatedNewer, ordered[4]);
    }
}
