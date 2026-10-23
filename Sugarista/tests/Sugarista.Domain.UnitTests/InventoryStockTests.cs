using Sugarista.Domain.Entities;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.UnitTests;

public class InventoryStockTests
{
    [Fact]
    public void RestockAndDeduct_ShouldUpdateCurrentQuantity()
    {
        var stock = new InventoryStock(Guid.NewGuid(), minAlertQuantity: 10m);

        stock.Restock(25m, "خرید اولیه");
        stock.Deduct(5m, "لاته");

        Assert.Equal(20m, stock.CurrentQuantity);
        Assert.False(stock.IsLowStock);
    }

    [Fact]
    public void Restore_ShouldReturnStockAfterOrderCancellation()
    {
        var stock = new InventoryStock(Guid.NewGuid());
        stock.Restock(12m, "خرید");

        stock.Deduct(4m, "لاته");
        stock.Restore(4m, "لاته", "لغو سفارش");

        Assert.Equal(12m, stock.CurrentQuantity);
    }

    [Fact]
    public void IsLowStock_ShouldUseConfiguredThreshold()
    {
        var stock = new InventoryStock(Guid.NewGuid(), minAlertQuantity: 5m);
        stock.Restock(4m, "خرید");

        Assert.True(stock.IsLowStock);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Deduct_ShouldRejectNonPositiveQuantity(decimal quantity)
    {
        var stock = new InventoryStock(Guid.NewGuid());
        stock.Restock(10m, "خرید");

        Assert.Throws<DomainException>(() => stock.Deduct(quantity, "لاته"));
    }

    [Fact]
    public void Adjust_ShouldRejectZeroDelta()
    {
        var stock = new InventoryStock(Guid.NewGuid());

        Assert.Throws<DomainException>(() => stock.Adjust(0m, "اصلاح"));
    }

    [Fact]
    public void SetAlertThreshold_ShouldRejectNegativeValue()
    {
        var stock = new InventoryStock(Guid.NewGuid());

        Assert.Throws<DomainException>(() => stock.SetAlertThreshold(-1m));
    }
}
