using Sugarista.Domain.Entities;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.UnitTests;

public class MenuItemPriceHistoryTests
{
    [Fact]
    public void Create_ComputesExactPurchaseAndSaleDeltasAndPercentages()
    {
        var menuItemId = Guid.NewGuid();
        var history = MenuItemPriceHistory.Create(
            menuItemId,
            previousPurchasePrice: 100_000m,
            newPurchasePrice: 120_000m,
            previousSalePrice: 200_000m,
            newSalePrice: 230_000m,
            source: MenuItemPriceHistory.SourceManualEdit,
            note: "افزایش قیمت تامین‌کننده کیک",
            changedBy: "admin");

        Assert.Equal(menuItemId, history.MenuItemId);
        Assert.Equal(100_000m, history.PreviousPurchasePrice);
        Assert.Equal(120_000m, history.NewPurchasePrice);
        Assert.Equal(20_000m, history.PurchasePriceDelta);
        Assert.Equal(20.00m, history.PurchasePriceChangePercent);
        Assert.Equal(30_000m, history.SalePriceDelta);
        Assert.Equal(15.00m, history.SalePriceChangePercent);
        Assert.Equal("افزایش قیمت تامین‌کننده کیک", history.Note);
        Assert.Equal("admin", history.ChangedBy);
    }

    [Fact]
    public void MenuItem_SetPurchasePrice_RejectsNegativeAndAllowsNull()
    {
        var item = new MenuItem(
            Guid.NewGuid(),
            "چیزکیک سن‌سباستین",
            "San Sebastian Cheesecake",
            "کیک تازه",
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

        item.SetPurchasePrice(100_000m);
        Assert.Equal(100_000m, item.PurchasePrice);

        item.SetPurchasePrice(null);
        Assert.Null(item.PurchasePrice);

        Assert.Throws<DomainException>(() => item.SetPurchasePrice(-5_000m));
    }

    [Fact]
    public void MenuItemStock_SetStock_UpdatesQuantityAndLowStockFlag()
    {
        var stock = new MenuItemStock(Guid.NewGuid(), minAlertQuantity: 2m);
        stock.SetStock(10m, 3m);

        Assert.Equal(10m, stock.CurrentQuantity);
        Assert.Equal(3m, stock.MinAlertQuantity);
        Assert.False(stock.IsLowStock);

        stock.DeductForSale(8m);
        Assert.Equal(2m, stock.CurrentQuantity);
        Assert.True(stock.IsLowStock);

        Assert.Throws<DomainException>(() => stock.SetStock(-1m, 3m));
        Assert.Throws<DomainException>(() => stock.SetStock(5m, -1m));
    }
}
