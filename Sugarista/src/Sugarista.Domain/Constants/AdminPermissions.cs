namespace Sugarista.Domain.Constants;

public static class AdminPermissions
{
    public const string ClaimType = "permission";
    public const string SuperAdminClaimType = "is_super_admin";

    public static class Dashboard
    {
        public const string View = "admin.dashboard.view";
        public const string Update = "admin.dashboard.update";
    }

    public static class Salon
    {
        public const string View = "admin.salon.view";
        public const string Create = "admin.salon.create";
        public const string Update = "admin.salon.update";
        public const string Delete = "admin.salon.delete";
    }

    public static class Categories
    {
        public const string View = "admin.categories.view";
        public const string Create = "admin.categories.create";
        public const string Update = "admin.categories.update";
        public const string Delete = "admin.categories.delete";
    }

    public static class MenuItems
    {
        public const string View = "admin.menu-items.view";
        public const string Create = "admin.menu-items.create";
        public const string Update = "admin.menu-items.update";
        public const string Delete = "admin.menu-items.delete";
    }

    public static class Addons
    {
        public const string View = "admin.addons.view";
        public const string Create = "admin.addons.create";
        public const string Update = "admin.addons.update";
        public const string Delete = "admin.addons.delete";
    }

    public static class Modifiers
    {
        public const string View = "admin.modifiers.view";
        public const string Create = "admin.modifiers.create";
        public const string Update = "admin.modifiers.update";
        public const string Delete = "admin.modifiers.delete";
    }

    public static class Ingredients
    {
        public const string View = "admin.ingredients.view";
        public const string Create = "admin.ingredients.create";
        public const string Update = "admin.ingredients.update";
        public const string Delete = "admin.ingredients.delete";
    }

    public static class Suppliers
    {
        public const string View = "admin.suppliers.view";
        public const string Create = "admin.suppliers.create";
        public const string Update = "admin.suppliers.update";
        public const string Delete = "admin.suppliers.delete";
    }

    public static class Recipes
    {
        public const string View = "admin.recipes.view";
        public const string Create = "admin.recipes.create";
        public const string Update = "admin.recipes.update";
        public const string Delete = "admin.recipes.delete";
    }

    public static class ProductWiki
    {
        public const string View = "admin.product-wiki.view";
        public const string Update = "admin.product-wiki.update";
    }

    public static class Inventory
    {
        public const string View = "admin.inventory.view";
        public const string Create = "admin.inventory.create";
        public const string Update = "admin.inventory.update";
    }

    public static class InventoryPurchases
    {
        public const string View = "admin.inventory-purchases.view";
        public const string Create = "admin.inventory-purchases.create";
    }

    public static class Uploads
    {
        public const string Create = "admin.uploads.create";
    }

    public static class Production
    {
        public const string View = "admin.production.view";
        public const string Create = "admin.production.create";
    }

    public static class RecordSale
    {
        public const string View = "admin.record-sale.view";
        public const string Create = "admin.record-sale.create";
    }

    public static class Orders
    {
        public const string View = "admin.orders.view";
        public const string Update = "admin.orders.update";
        public const string Delete = "admin.orders.delete";
    }

    public static class Pos
    {
        public const string View = "admin.pos.view";
        public const string Create = "admin.pos.create";
        public const string Update = "admin.pos.update";
        public const string Delete = "admin.pos.delete";
        public const string Pay = "admin.pos.pay";
    }

    public static class Hr
    {
        public const string View = "admin.hr.view";
        public const string Create = "admin.hr.create";
        public const string Update = "admin.hr.update";
        public const string Delete = "admin.hr.delete";
    }

    public static class Assets
    {
        public const string View = "admin.assets.view";
        public const string Create = "admin.assets.create";
        public const string Update = "admin.assets.update";
        public const string Delete = "admin.assets.delete";
    }

    public static class Accounting
    {
        public const string View = "admin.accounting.view";
        public const string Create = "admin.accounting.create";
        public const string Update = "admin.accounting.update";
        public const string Delete = "admin.accounting.delete";
    }

    public static class SiteSettings
    {
        public const string View = "admin.site-settings.view";
        public const string Update = "admin.site-settings.update";
    }

    public static class Users
    {
        public const string View = "admin.users.view";
        public const string Create = "admin.users.create";
        public const string Update = "admin.users.update";
        public const string Delete = "admin.users.delete";
        public const string ManagePermissions = "admin.users.manage-permissions";
    }

    public static class ActivityLogs
    {
        public const string View = "admin.activity-logs.view";
        public const string Delete = "admin.activity-logs.delete";
    }

    public static readonly IReadOnlyList<AdminPermissionDefinition> Catalog =
    [
        new(Dashboard.View, "dashboard", "view", "نمای کلی", "مشاهده داشبورد"),
        new(Dashboard.Update, "dashboard", "update", "کاست کنترل", "ذخیره تنظیمات کاست کنترل و قیمت‌گذاری"),
        new(Salon.View, "salon", "view", "وضعیت سالن", "مشاهده"),
        new(Salon.Create, "salon", "create", "وضعیت سالن", "ایجاد میز"),
        new(Salon.Update, "salon", "update", "وضعیت سالن", "ویرایش / اشغال / آزادسازی"),
        new(Salon.Delete, "salon", "delete", "وضعیت سالن", "حذف میز"),
        new(Categories.View, "categories", "view", "دسته‌بندی‌ها", "مشاهده"),
        new(Categories.Create, "categories", "create", "دسته‌بندی‌ها", "ایجاد"),
        new(Categories.Update, "categories", "update", "دسته‌بندی‌ها", "ویرایش"),
        new(Categories.Delete, "categories", "delete", "دسته‌بندی‌ها", "حذف"),
        new(MenuItems.View, "menu-items", "view", "آیتم‌های منو", "مشاهده"),
        new(MenuItems.Create, "menu-items", "create", "آیتم‌های منو", "ایجاد"),
        new(MenuItems.Update, "menu-items", "update", "آیتم‌های منو", "ویرایش"),
        new(MenuItems.Delete, "menu-items", "delete", "آیتم‌های منو", "حذف"),
        new(Addons.View, "addons", "view", "افزودنی‌ها", "مشاهده"),
        new(Addons.Create, "addons", "create", "افزودنی‌ها", "ایجاد"),
        new(Addons.Update, "addons", "update", "افزودنی‌ها", "ویرایش"),
        new(Addons.Delete, "addons", "delete", "افزودنی‌ها", "حذف"),
        new(Modifiers.View, "modifiers", "view", "تغییرات سفارش", "مشاهده"),
        new(Modifiers.Create, "modifiers", "create", "تغییرات سفارش", "ایجاد"),
        new(Modifiers.Update, "modifiers", "update", "تغییرات سفارش", "ویرایش"),
        new(Modifiers.Delete, "modifiers", "delete", "تغییرات سفارش", "حذف"),
        new(Ingredients.View, "ingredients", "view", "مواد اولیه", "مشاهده"),
        new(Ingredients.Create, "ingredients", "create", "مواد اولیه", "ایجاد"),
        new(Ingredients.Update, "ingredients", "update", "مواد اولیه", "ویرایش"),
        new(Ingredients.Delete, "ingredients", "delete", "مواد اولیه", "حذف"),
        new(Suppliers.View, "suppliers", "view", "تامین‌کنندگان", "مشاهده"),
        new(Suppliers.Create, "suppliers", "create", "تامین‌کنندگان", "ایجاد"),
        new(Suppliers.Update, "suppliers", "update", "تامین‌کنندگان", "ویرایش"),
        new(Suppliers.Delete, "suppliers", "delete", "تامین‌کنندگان", "حذف"),
        new(Recipes.View, "recipes", "view", "دستور پخت", "مشاهده"),
        new(Recipes.Create, "recipes", "create", "دستور پخت", "افزودن خط"),
        new(Recipes.Update, "recipes", "update", "دستور پخت", "ویرایش"),
        new(Recipes.Delete, "recipes", "delete", "دستور پخت", "حذف خط"),
        new(ProductWiki.View, "product-wiki", "view", "ویکی محصولات", "مشاهده"),
        new(ProductWiki.Update, "product-wiki", "update", "ویکی محصولات", "ویرایش"),
        new(Inventory.View, "inventory", "view", "انبار موجودی", "مشاهده"),
        new(Inventory.Create, "inventory", "create", "انبار موجودی", "ورود / شارژ"),
        new(Inventory.Update, "inventory", "update", "انبار موجودی", "تعدیل / آستانه"),
        new(InventoryPurchases.View, "inventory-purchases", "view", "خرید مواد", "مشاهده"),
        new(InventoryPurchases.Create, "inventory-purchases", "create", "خرید مواد", "ثبت خرید"),
        new(Uploads.Create, "uploads", "create", "آپلود فایل", "آپلود سند انبار"),
        new(Production.View, "production", "view", "تولید", "مشاهده"),
        new(Production.Create, "production", "create", "تولید", "ثبت تولید"),
        new(RecordSale.View, "record-sale", "view", "ثبت فروش", "مشاهده"),
        new(RecordSale.Create, "record-sale", "create", "ثبت فروش", "ثبت"),
        new(Orders.View, "orders", "view", "سفارش‌ها", "مشاهده"),
        new(Orders.Update, "orders", "update", "سفارش‌ها", "ویرایش / آماده‌سازی"),
        new(Orders.Delete, "orders", "delete", "سفارش‌ها", "لغو"),
        new(Pos.View, "pos", "view", "صندوق فروش", "مشاهده"),
        new(Pos.Create, "pos", "create", "صندوق فروش", "ثبت / نگه‌داشت"),
        new(Pos.Update, "pos", "update", "صندوق فروش", "ویرایش سفارش"),
        new(Pos.Delete, "pos", "delete", "صندوق فروش", "حذف سفارش"),
        new(Pos.Pay, "pos", "pay", "صندوق فروش", "دریافت پرداخت"),
        new(Accounting.View, "accounting", "view", "حسابداری", "مشاهده"),
        new(Accounting.Create, "accounting", "create", "حسابداری", "ثبت سند / سرمایه"),
        new(Accounting.Update, "accounting", "update", "حسابداری", "ویرایش شرکا / تقسیم سود"),
        new(Accounting.Delete, "accounting", "delete", "حسابداری", "حذف / لغو"),
        new(Hr.View, "hr", "view", "منابع انسانی", "مشاهده پرسنل و حقوق"),
        new(Hr.Create, "hr", "create", "منابع انسانی", "ثبت پرسنل / ساختار پرداخت"),
        new(Hr.Update, "hr", "update", "منابع انسانی", "ویرایش / حضور و پرداخت حقوق"),
        new(Hr.Delete, "hr", "delete", "منابع انسانی", "حذف / غیرفعال‌سازی"),
        new(Assets.View, "assets", "view", "اموال و تجهیزات", "مشاهدهٔ اموال"),
        new(Assets.Create, "assets", "create", "اموال و تجهیزات", "ثبت قلم / بارگذاری عکس"),
        new(Assets.Update, "assets", "update", "اموال و تجهیزات", "ویرایش / خارج از رده کردن"),
        new(Assets.Delete, "assets", "delete", "اموال و تجهیزات", "حذف قلم / حذف عکس"),
        new(SiteSettings.View, "site-settings", "view", "تنظیمات سایت", "مشاهده"),
        new(SiteSettings.Update, "site-settings", "update", "تنظیمات سایت", "ویرایش"),
        new(ActivityLogs.View, "activity-logs", "view", "لاگ فعالیت‌ها", "مشاهدهٔ لاگ کاربران"),
        new(ActivityLogs.Delete, "activity-logs", "delete", "لاگ فعالیت‌ها", "پاک‌سازی لاگ‌های قدیمی"),
        new(Users.View, "admin-users", "view", "مدیریت کاربران", "مشاهده"),
        new(Users.Create, "admin-users", "create", "مدیریت کاربران", "ایجاد کاربر"),
        new(Users.Update, "admin-users", "update", "مدیریت کاربران", "ویرایش کاربر"),
        new(Users.Delete, "admin-users", "delete", "مدیریت کاربران", "غیرفعال‌سازی"),
        new(Users.ManagePermissions, "admin-users", "manage-permissions", "مدیریت کاربران", "مدیریت دسترسی‌ها"),
    ];

    public static readonly HashSet<string> AllKeys = Catalog
        .Select(permission => permission.Key)
        .ToHashSet(StringComparer.Ordinal);

    public static bool IsValid(string? permissionKey) =>
        !string.IsNullOrWhiteSpace(permissionKey) && AllKeys.Contains(permissionKey);

    public static string ViewPermissionForSection(string sectionId) =>
        Catalog.First(permission => permission.SectionId == sectionId && permission.Action == "view").Key;
}

public sealed record AdminPermissionDefinition(
    string Key,
    string SectionId,
    string Action,
    string SectionLabel,
    string ActionLabel);
