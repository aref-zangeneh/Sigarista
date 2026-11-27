using Sugarista.Domain.Constants;

namespace Sugarista.Domain.UnitTests;

public class AdminPermissionsCatalogTests
{
    [Fact]
    public void Catalog_HasNoDuplicateKeys_AndEveryEntryHasCategory()
    {
        var keys = AdminPermissions.Catalog.Select(p => p.Key).ToList();
        Assert.Equal(keys.Count, keys.Distinct(StringComparer.Ordinal).Count());

        foreach (var entry in AdminPermissions.Catalog)
        {
            Assert.False(string.IsNullOrWhiteSpace(entry.Key));
            Assert.False(string.IsNullOrWhiteSpace(entry.SectionId));
            Assert.False(string.IsNullOrWhiteSpace(entry.SectionLabel));
            Assert.False(string.IsNullOrWhiteSpace(entry.CategoryId));
            Assert.False(string.IsNullOrWhiteSpace(entry.CategoryLabel));
            Assert.NotEqual("other", entry.CategoryId);
            Assert.True(AdminPermissions.IsValid(entry.Key));
        }
    }

    [Fact]
    public void EveryMenuSection_HasItsOwnIndependentViewPermission()
    {
        var independentViews = new[]
        {
            AdminPermissions.Dashboard.View,
            AdminPermissions.CostDashboard.View,
            AdminPermissions.Orders.View,
            AdminPermissions.Pos.View,
            AdminPermissions.DeliveryMethods.View,
            AdminPermissions.Customers.View,
            AdminPermissions.Salon.View,
            AdminPermissions.Categories.View,
            AdminPermissions.MenuItems.View,
            AdminPermissions.QuickStock.View,
            AdminPermissions.OptionTemplates.View,
            AdminPermissions.Addons.View,
            AdminPermissions.DrinkBuilder.View,
            AdminPermissions.Discounts.View,
            AdminPermissions.Modifiers.View,
            AdminPermissions.Blog.View,
            AdminPermissions.Ingredients.View,
            AdminPermissions.Recipes.View,
            AdminPermissions.Production.View,
            AdminPermissions.ProductWiki.View,
            AdminPermissions.Warehouses.View,
            AdminPermissions.WarehouseVouchers.View,
            AdminPermissions.Inventory.View,
            AdminPermissions.InventoryPurchases.View,
            AdminPermissions.Suppliers.View,
            AdminPermissions.Assets.View,
            AdminPermissions.Maintenance.View,
            AdminPermissions.RecordSale.View,
            AdminPermissions.Accounting.View,
            AdminPermissions.Exports.View,
            AdminPermissions.Hr.View,
            AdminPermissions.StaffTasks.View,
            AdminPermissions.SiteSettings.View,
            AdminPermissions.SpecialRoutes.View,
            AdminPermissions.Tooltips.View,
            AdminPermissions.Users.View,
            AdminPermissions.ActivityLogs.View,
            AdminPermissions.SystemLogs.View,
        };

        // Every menu item must have a unique, non-overlapping view permission
        Assert.Equal(independentViews.Length, independentViews.Distinct(StringComparer.Ordinal).Count());
        foreach (var key in independentViews)
        {
            Assert.True(AdminPermissions.IsValid(key), $"Permission '{key}' must exist in AdminPermissions.Catalog.");
        }
    }
}
