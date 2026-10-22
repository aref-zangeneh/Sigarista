using Sugarista.Domain.Constants;

namespace Sugarista.Domain.Constants;

/// <summary>
/// Permission bundles of the four seeded system roles. Kept in the Domain so
/// the seeder and tools share one source of truth. Editing the live bundles
/// happens in the UI (admin roles screen) — these presets only decide what a
/// fresh install starts with.
/// </summary>
public static class DefaultRolePermissions
{
    /// <summary>Everything a full operator needs on the floor — users stays super-admin-only.</summary>
    public static readonly string[] Admin =
    [
        AdminPermissions.Dashboard.View,
        AdminPermissions.Dashboard.Update,
        AdminPermissions.Salon.View,
        AdminPermissions.Salon.Create,
        AdminPermissions.Salon.Update,
        AdminPermissions.Salon.Delete,
        AdminPermissions.Categories.View,
        AdminPermissions.Categories.Create,
        AdminPermissions.Categories.Update,
        AdminPermissions.Categories.Delete,
        AdminPermissions.MenuItems.View,
        AdminPermissions.MenuItems.Create,
        AdminPermissions.MenuItems.Update,
        AdminPermissions.MenuItems.Delete,
        AdminPermissions.Addons.View,
        AdminPermissions.Addons.Create,
        AdminPermissions.Addons.Update,
        AdminPermissions.Addons.Delete,
        AdminPermissions.Modifiers.View,
        AdminPermissions.Modifiers.Create,
        AdminPermissions.Modifiers.Update,
        AdminPermissions.Modifiers.Delete,
        AdminPermissions.Ingredients.View,
        AdminPermissions.Ingredients.Create,
        AdminPermissions.Ingredients.Update,
        AdminPermissions.Ingredients.Delete,
        AdminPermissions.Suppliers.View,
        AdminPermissions.Suppliers.Create,
        AdminPermissions.Suppliers.Update,
        AdminPermissions.Suppliers.Delete,
        AdminPermissions.Recipes.View,
        AdminPermissions.Recipes.Create,
        AdminPermissions.Recipes.Update,
        AdminPermissions.Recipes.Delete,
        AdminPermissions.ProductWiki.View,
        AdminPermissions.ProductWiki.Update,
        AdminPermissions.Inventory.View,
        AdminPermissions.Inventory.Create,
        AdminPermissions.Inventory.Update,
        AdminPermissions.InventoryPurchases.View,
        AdminPermissions.InventoryPurchases.Create,
        AdminPermissions.Uploads.Create,
        AdminPermissions.Production.View,
        AdminPermissions.Production.Create,
        AdminPermissions.RecordSale.View,
        AdminPermissions.RecordSale.Create,
        AdminPermissions.Orders.View,
        AdminPermissions.Orders.Update,
        AdminPermissions.Orders.Delete,
        AdminPermissions.Pos.View,
        AdminPermissions.Pos.Create,
        AdminPermissions.Pos.Update,
        AdminPermissions.Pos.Pay,
        AdminPermissions.Hr.View,
        AdminPermissions.Hr.Create,
        AdminPermissions.Hr.Update,
        AdminPermissions.Hr.Delete,
        AdminPermissions.Assets.View,
        AdminPermissions.Assets.Create,
        AdminPermissions.Assets.Update,
        AdminPermissions.Assets.Delete,
        AdminPermissions.Accounting.View,
        AdminPermissions.Accounting.Create,
        AdminPermissions.Accounting.Update,
        AdminPermissions.SiteSettings.View,
        AdminPermissions.SiteSettings.Update,
        AdminPermissions.ActivityLogs.View,
        AdminPermissions.ActivityLogs.Delete,
    ];

    /// <summary>Sales floor: POS, orders, own dashboard. No finance, no HR, no inventory.</summary>
    public static readonly string[] Cashier =
    [
        AdminPermissions.Dashboard.View,
        AdminPermissions.RecordSale.View,
        AdminPermissions.RecordSale.Create,
        AdminPermissions.Orders.View,
        AdminPermissions.Orders.Update,
        AdminPermissions.Pos.View,
        AdminPermissions.Pos.Create,
        AdminPermissions.Pos.Update,
        AdminPermissions.Pos.Pay,
        AdminPermissions.MenuItems.View,
        AdminPermissions.Orders.Delete,
    ];

    /// <summary>Warehouse: stock, purchases, production, recipes. No sales money, no payroll.</summary>
    public static readonly string[] InventoryManager =
    [
        AdminPermissions.Dashboard.View,
        AdminPermissions.Ingredients.View,
        AdminPermissions.Ingredients.Create,
        AdminPermissions.Ingredients.Update,
        AdminPermissions.Ingredients.Delete,
        AdminPermissions.Suppliers.View,
        AdminPermissions.Suppliers.Create,
        AdminPermissions.Suppliers.Update,
        AdminPermissions.Suppliers.Delete,
        AdminPermissions.Inventory.View,
        AdminPermissions.Inventory.Create,
        AdminPermissions.Inventory.Update,
        AdminPermissions.InventoryPurchases.View,
        AdminPermissions.InventoryPurchases.Create,
        AdminPermissions.Production.View,
        AdminPermissions.Production.Create,
        AdminPermissions.Recipes.View,
        AdminPermissions.Recipes.Create,
        AdminPermissions.Recipes.Update,
        AdminPermissions.ProductWiki.View,
        AdminPermissions.ProductWiki.Update,
        AdminPermissions.Categories.View,
        AdminPermissions.MenuItems.View,
        AdminPermissions.Salon.View,
        AdminPermissions.Assets.View,
        AdminPermissions.Assets.Create,
        AdminPermissions.Assets.Update,
    ];

    /// <summary>People: HR module plus read-only accounting (فیش‌ها روی سند حسابداری اثر می‌گذارند).</summary>
    public static readonly string[] Hr =
    [
        AdminPermissions.Dashboard.View,
        AdminPermissions.Hr.View,
        AdminPermissions.Hr.Create,
        AdminPermissions.Hr.Update,
        AdminPermissions.Hr.Delete,
        AdminPermissions.Accounting.View,
    ];
}
