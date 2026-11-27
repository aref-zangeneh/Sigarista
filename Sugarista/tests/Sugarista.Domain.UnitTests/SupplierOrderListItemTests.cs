using Sugarista.Domain.Entities;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.UnitTests;

public class SupplierOrderListItemTests
{
    [Fact]
    public void Constructor_ValidInputs_CreatesPendingItem()
    {
        var menuItemId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();

        var item = new SupplierOrderListItem(
            itemName: " کیک اپرا ",
            quantity: 6,
            unit: "عدد",
            menuItemId: menuItemId,
            categoryName: "کیک و دسر",
            supplierId: supplierId,
            notes: "تازه و برش خورده",
            currentStockSnapshot: 0);

        Assert.Equal("کیک اپرا", item.ItemName);
        Assert.Equal(6, item.Quantity);
        Assert.Equal("عدد", item.Unit);
        Assert.Equal(menuItemId, item.MenuItemId);
        Assert.Equal(supplierId, item.SupplierId);
        Assert.Equal(SupplierOrderListItemStatus.Pending, item.Status);
        Assert.Equal(0, item.CurrentStockSnapshot);
    }

    [Fact]
    public void Constructor_EmptyName_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => new SupplierOrderListItem("   ", 2));
    }

    [Fact]
    public void Constructor_NonPositiveQuantity_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => new SupplierOrderListItem("دنیش توت‌فرنگی", 0));
    }

    [Fact]
    public void Lifecycle_PendingToOrderedToReceived_UpdatesStatusAndTimestamps()
    {
        var item = new SupplierOrderListItem("دنیش توت‌فرنگی", 10, "عدد");
        var orderedAt = new DateTime(2026, 11, 28, 10, 0, 0, DateTimeKind.Utc);
        var receivedAt = orderedAt.AddHours(4);

        item.AddQuantity(4, latestStockSnapshot: 0);
        Assert.Equal(14, item.Quantity);
        Assert.Equal(0, item.CurrentStockSnapshot);

        item.MarkOrdered("PO-20261128-1000", orderedAt);
        Assert.Equal(SupplierOrderListItemStatus.Ordered, item.Status);
        Assert.Equal("PO-20261128-1000", item.BatchCode);
        Assert.Equal(orderedAt, item.OrderedAt);

        item.MarkReceived(receivedAt);
        Assert.Equal(SupplierOrderListItemStatus.Received, item.Status);
        Assert.Equal(receivedAt, item.ReceivedAt);
    }
}
