using Sugarista.Domain.Constants;

namespace Sugarista.Domain.UnitTests;

public class AdminPermissionsTests
{
    [Fact]
    public void Catalog_ShouldNotContainDuplicateKeys()
    {
        Assert.Equal(AdminPermissions.Catalog.Count, AdminPermissions.AllKeys.Count);
    }

    [Fact]
    public void Catalog_ShouldUseConsistentPermissionKeyFormat()
    {
        foreach (var permission in AdminPermissions.Catalog)
        {
            Assert.StartsWith("admin.", permission.Key);
            Assert.EndsWith($".{permission.Action}", permission.Key);
            Assert.Equal(3, permission.Key.Split('.').Length);
        }
    }

    [Fact]
    public void ViewPermissionForSection_ShouldReturnViewKey()
    {
        Assert.Equal(
            AdminPermissions.Orders.View,
            AdminPermissions.ViewPermissionForSection("orders"));
    }

    [Fact]
    public void IsValid_ShouldRejectUnknownPermission()
    {
        Assert.False(AdminPermissions.IsValid("admin.orders.superpower"));
        Assert.False(AdminPermissions.IsValid(null));
    }
}
