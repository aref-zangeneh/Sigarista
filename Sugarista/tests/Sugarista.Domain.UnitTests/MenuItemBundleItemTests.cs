using Sugarista.Domain.Entities;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.UnitTests;

public class MenuItemBundleItemTests
{
    [Fact]
    public void Constructor_ValidInputs_NormalizesSizeKeyAndSetsProperties()
    {
        var bundleId = Guid.NewGuid();
        var compId = Guid.NewGuid();

        var entry = new MenuItemBundleItem(bundleId, compId, quantity: 2, sizeKey: " Medium ", displayOrder: 3);

        Assert.Equal(bundleId, entry.BundleMenuItemId);
        Assert.Equal(compId, entry.ComponentMenuItemId);
        Assert.Equal(2, entry.Quantity);
        Assert.Equal("medium", entry.SizeKey);
        Assert.Equal(3, entry.DisplayOrder);
    }

    [Fact]
    public void Constructor_SelfReference_ThrowsDomainException()
    {
        var id = Guid.NewGuid();
        Assert.Throws<DomainException>(() => new MenuItemBundleItem(id, id, quantity: 1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    [InlineData(51)]
    public void Constructor_InvalidQuantity_ThrowsDomainException(int quantity)
    {
        Assert.Throws<DomainException>(() =>
            new MenuItemBundleItem(Guid.NewGuid(), Guid.NewGuid(), quantity: quantity));
    }

    [Theory]
    [InlineData("xl")]
    [InlineData("giant")]
    public void Constructor_InvalidSizeKey_ThrowsDomainException(string sizeKey)
    {
        Assert.Throws<DomainException>(() =>
            new MenuItemBundleItem(Guid.NewGuid(), Guid.NewGuid(), quantity: 1, sizeKey: sizeKey));
    }

    [Fact]
    public void Constructor_NegativeDisplayOrder_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() =>
            new MenuItemBundleItem(Guid.NewGuid(), Guid.NewGuid(), quantity: 1, displayOrder: -1));
    }

    [Fact]
    public void OrderLine_StoresImmutableBundleSnapshotJson()
    {
        var line = new OrderLine(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "باندل صبحانه",
            "Morning Bundle",
            null,
            null,
            1,
            430_000m,
            [],
            bundleSnapshotJson: "{\"regularTotal\":500000,\"bundleUnitPrice\":430000}");

        Assert.Equal("{\"regularTotal\":500000,\"bundleUnitPrice\":430000}", line.BundleSnapshotJson);
    }
}
