using Sugarista.Application.MenuItems.Queries.GetMenuItemPriceHistory;
using Sugarista.Application.MenuItems.Services;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.UnitTests.MenuItems;

public class MenuItemPriceTrendTests
{
    [Fact]
    public void ComputeCostPerServing_FallsBackToPurchasePriceWhenNoRecipe()
    {
        var cake = new MenuItem(
            Guid.NewGuid(),
            "چیزکیک سن‌سباستین",
            "San Sebastian",
            "",
            null,
            [],
            null,
            0m,
            true,
            null,
            null,
            [],
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            [],
            null,
            false,
            "",
            "SC",
            0,
            true,
            200_000m);

        cake.SetPurchasePrice(100_000m);

        Assert.Equal(100_000m, MenuPricingService.ComputeCostPerServing(cake));
    }

    [Fact]
    public void ComputeTrendSummary_TracksInflationFrom100kTo120kTo135k()
    {
        var itemId = Guid.NewGuid();
        var t1 = new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);
        var t2 = new DateTimeOffset(2026, 10, 15, 9, 0, 0, TimeSpan.Zero);
        var t3 = new DateTimeOffset(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);

        var e1 = MenuItemPriceHistory.Create(itemId, null, 100_000m, null, 200_000m, MenuItemPriceHistory.SourceInitial, null, "admin", t1);
        var e2 = MenuItemPriceHistory.Create(itemId, 100_000m, 120_000m, 200_000m, 200_000m, MenuItemPriceHistory.SourceManualEdit, "گرانی اول", "admin", t2);
        var e3 = MenuItemPriceHistory.Create(itemId, 120_000m, 135_000m, 200_000m, 240_000m, MenuItemPriceHistory.SourceManualEdit, "گرانی دوم", "admin", t3);

        // Input is newest-first
        var summary = GetMenuItemPriceHistoryQueryHandler.ComputeTrendSummary([e3, e2, e1], 135_000m, 240_000m);

        Assert.Equal(100_000m, summary.FirstPurchasePrice);
        Assert.Equal(135_000m, summary.LatestPurchasePrice);
        Assert.Equal(35_000m, summary.TotalPurchaseChange);
        Assert.Equal(35.00m, summary.TotalPurchaseChangePercent);
        Assert.Equal(100_000m, summary.MinPurchasePrice);
        Assert.Equal(135_000m, summary.MaxPurchasePrice);
        Assert.Equal(200_000m, summary.FirstSalePrice);
        Assert.Equal(240_000m, summary.LatestSalePrice);
        Assert.Equal(40_000m, summary.TotalSaleChange);
        Assert.Equal(20.00m, summary.TotalSaleChangePercent);
        Assert.Equal(3, summary.EntryCount);
    }
}
