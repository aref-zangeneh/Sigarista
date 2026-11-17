using Sugarista.Domain.Entities;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.UnitTests.WarehouseTests;

public class WarehouseTests
{
    [Fact]
    public void Constructor_WithValidName_SetsProperties()
    {
        var warehouse = new Warehouse("انبار اصلی", code: "WH-1", description: "انبار مرکزی", displayOrder: 2);

        Assert.Equal("انبار اصلی", warehouse.Name);
        Assert.Equal("WH-1", warehouse.Code);
        Assert.Equal("انبار مرکزی", warehouse.Description);
        Assert.Equal(2, warehouse.DisplayOrder);
        Assert.True(warehouse.IsActive);
        Assert.Null(warehouse.ParentId);
        Assert.False(warehouse.IsSubWarehouse);
    }

    [Fact]
    public void Constructor_WithParent_BecomesSubWarehouse()
    {
        var parentId = Guid.NewGuid();
        var warehouse = new Warehouse("انبارک قفسه A", parentId: parentId);

        Assert.Equal(parentId, warehouse.ParentId);
        Assert.True(warehouse.IsSubWarehouse);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithEmptyName_Throws(string? name)
    {
        Assert.Throws<DomainException>(() => new Warehouse(name!));
    }

    [Fact]
    public void Constructor_WithTooLongName_Throws()
    {
        Assert.Throws<DomainException>(() => new Warehouse(new string('ا', Warehouse.MaxNameLength + 1)));
    }

    [Fact]
    public void Constructor_WithEmptyParentId_Throws()
    {
        Assert.Throws<DomainException>(() => new Warehouse("انبار", parentId: Guid.Empty));
    }

    [Fact]
    public void Update_TrimsAndNormalizes()
    {
        var warehouse = new Warehouse("انبار اصلی");

        warehouse.Update("  انبار شمال  ", "  ", "  توضیحات  ", 3, isActive: false);

        Assert.Equal("انبار شمال", warehouse.Name);
        Assert.Null(warehouse.Code);
        Assert.Equal("توضیحات", warehouse.Description);
        Assert.Equal(3, warehouse.DisplayOrder);
        Assert.False(warehouse.IsActive);
    }

    [Fact]
    public void MoveTo_EmptyGuid_Throws()
    {
        var warehouse = new Warehouse("انبار اصلی");

        Assert.Throws<DomainException>(() => warehouse.MoveTo(Guid.Empty));
    }

    [Fact]
    public void MoveTo_Null_GoesToTopLevel()
    {
        var warehouse = new Warehouse("انبارک", parentId: Guid.NewGuid());

        warehouse.MoveTo(null);

        Assert.Null(warehouse.ParentId);
        Assert.False(warehouse.IsSubWarehouse);
    }
}

public class WarehouseStockTests
{
    [Fact]
    public void Constructor_SetsZeroQuantity()
    {
        var stock = new WarehouseStock(Guid.NewGuid(), Guid.NewGuid(), minAlertQuantity: 5);

        Assert.Equal(0, stock.Quantity);
        Assert.Equal(5, stock.MinAlertQuantity);
        Assert.True(stock.IsLowStock);
    }

    [Fact]
    public void Constructor_WithEmptyWarehouse_Throws()
    {
        Assert.Throws<DomainException>(() => new WarehouseStock(Guid.Empty, Guid.NewGuid()));
    }

    [Fact]
    public void Constructor_WithEmptyIngredient_Throws()
    {
        Assert.Throws<DomainException>(() => new WarehouseStock(Guid.NewGuid(), Guid.Empty));
    }

    [Fact]
    public void Restock_IncreasesQuantity_AndReturnsMovementTaggedWithWarehouse()
    {
        var warehouseId = Guid.NewGuid();
        var stock = new WarehouseStock(warehouseId, Guid.NewGuid());

        var movement = stock.Restock(10, "خرید اول");

        Assert.Equal(10, stock.Quantity);
        Assert.False(stock.IsLowStock);
        Assert.Equal(10, movement.Quantity);
        Assert.Equal(StockMovementType.Restock, movement.MovementType);
        Assert.Equal(warehouseId, movement.WarehouseId);
        Assert.Equal("خرید اول", movement.Note);
    }

    [Fact]
    public void Restock_NonPositive_Throws()
    {
        var stock = new WarehouseStock(Guid.NewGuid(), Guid.NewGuid());

        Assert.Throws<DomainException>(() => stock.Restock(0, null));
        Assert.Throws<DomainException>(() => stock.Restock(-3, null));
    }

    [Fact]
    public void Adjust_MovesQuantity_BothDirections()
    {
        var stock = new WarehouseStock(Guid.NewGuid(), Guid.NewGuid());

        stock.Restock(10, null);
        var down = stock.Adjust(-4, "اتلاف");

        Assert.Equal(6, stock.Quantity);
        Assert.Equal(-4, down.Quantity);
        Assert.Equal(StockMovementType.ManualAdjustment, down.MovementType);
    }

    [Fact]
    public void Adjust_Zero_Throws()
    {
        var stock = new WarehouseStock(Guid.NewGuid(), Guid.NewGuid());

        Assert.Throws<DomainException>(() => stock.Adjust(0, null));
    }

    [Fact]
    public void SetAlertThreshold_Negative_Throws()
    {
        var stock = new WarehouseStock(Guid.NewGuid(), Guid.NewGuid());

        Assert.Throws<DomainException>(() => stock.SetAlertThreshold(-1));
    }

    [Fact]
    public void SetAlertThreshold_Valid_LowStockFlagReacts()
    {
        var stock = new WarehouseStock(Guid.NewGuid(), Guid.NewGuid());

        stock.Restock(5, null);
        stock.SetAlertThreshold(10);

        Assert.True(stock.IsLowStock);
        stock.SetAlertThreshold(3);
        Assert.False(stock.IsLowStock);
    }
}
