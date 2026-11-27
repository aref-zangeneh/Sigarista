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

    public static class CostDashboard
    {
        public const string View = "admin.cost-dashboard.view";
        public const string Update = "admin.cost-dashboard.update";
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

    public static class QuickStock
    {
        public const string View = "admin.quick-stock.view";
        public const string Update = "admin.quick-stock.update";
    }

    public static class OptionTemplates
    {
        public const string View = "admin.option-templates.view";
        public const string Create = "admin.option-templates.create";
        public const string Update = "admin.option-templates.update";
        public const string Delete = "admin.option-templates.delete";
    }

    public static class Addons
    {
        public const string View = "admin.addons.view";
        public const string Create = "admin.addons.create";
        public const string Update = "admin.addons.update";
        public const string Delete = "admin.addons.delete";
    }

    public static class Discounts
    {
        public const string View = "admin.discounts.view";
        public const string Create = "admin.discounts.create";
        public const string Update = "admin.discounts.update";
        public const string Delete = "admin.discounts.delete";
    }

    public static class Modifiers
    {
        public const string View = "admin.modifiers.view";
        public const string Create = "admin.modifiers.create";
        public const string Update = "admin.modifiers.update";
        public const string Delete = "admin.modifiers.delete";
    }

    public static class DrinkBuilder
    {
        public const string View = "admin.drink-builder.view";
        public const string Create = "admin.drink-builder.create";
        public const string Update = "admin.drink-builder.update";
        public const string Delete = "admin.drink-builder.delete";
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

    public static class Blog
    {
        public const string View = "admin.blog.view";
        public const string Create = "admin.blog.create";
        public const string Update = "admin.blog.update";
        public const string Delete = "admin.blog.delete";
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

    public static class Warehouses
    {
        public const string View = "admin.warehouses.view";
        public const string Create = "admin.warehouses.create";
        public const string Update = "admin.warehouses.update";
        public const string Delete = "admin.warehouses.delete";
    }

    public static class WarehouseVouchers
    {
        public const string View = "admin.warehouse-vouchers.view";
        public const string Create = "admin.warehouse-vouchers.create";
        public const string Update = "admin.warehouse-vouchers.update";
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

    public static class Customers
    {
        public const string View = "admin.customers.view";
        public const string Create = "admin.customers.create";
        public const string Update = "admin.customers.update";
        public const string Delete = "admin.customers.delete";
    }

    public static class DeliveryMethods
    {
        public const string View = "admin.delivery-methods.view";
        public const string Create = "admin.delivery-methods.create";
        public const string Update = "admin.delivery-methods.update";
        public const string Delete = "admin.delivery-methods.delete";
    }

    public static class Pos
    {
        public const string View = "admin.pos.view";
        public const string Create = "admin.pos.create";
        public const string Update = "admin.pos.update";
        public const string Delete = "admin.pos.delete";
        public const string Pay = "admin.pos.pay";
        public const string ApplyManualDiscount = "admin.pos.apply-discount";
        public const string ManagerNote = "admin.pos.manager-note";
    }

    public static class Hr
    {
        public const string View = "admin.hr.view";
        public const string Create = "admin.hr.create";
        public const string Update = "admin.hr.update";
        public const string Delete = "admin.hr.delete";
    }

    public static class StaffTasks
    {
        public const string View = "admin.staff-tasks.view";
        public const string ViewAll = "admin.staff-tasks.view-all";
        public const string Create = "admin.staff-tasks.create";
        public const string Update = "admin.staff-tasks.update";
        public const string Delete = "admin.staff-tasks.delete";
        public const string ViewLogs = "admin.staff-tasks.view-logs";
    }

    public static class Assets
    {
        public const string View = "admin.assets.view";
        public const string Create = "admin.assets.create";
        public const string Update = "admin.assets.update";
        public const string Delete = "admin.assets.delete";
    }

    public static class Maintenance
    {
        public const string View = "admin.maintenance.view";
        public const string Create = "admin.maintenance.create";
        public const string Update = "admin.maintenance.update";
        public const string Delete = "admin.maintenance.delete";
    }

    public static class Accounting
    {
        public const string View = "admin.accounting.view";
        public const string Create = "admin.accounting.create";
        public const string Update = "admin.accounting.update";
        public const string Delete = "admin.accounting.delete";
    }

    public static class Exports
    {
        public const string View = "admin.exports.view";
    }

    public static class SiteSettings
    {
        public const string View = "admin.site-settings.view";
        public const string Update = "admin.site-settings.update";
    }

    public static class SpecialRoutes
    {
        public const string View = "admin.special-routes.view";
        public const string Update = "admin.special-routes.update";
    }

    public static class Tooltips
    {
        public const string View = "admin.tooltips.view";
        public const string Update = "admin.tooltips.update";
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

    /// <summary>Server system logs (Serilog files on disk), browsable from the admin panel.</summary>
    public static class SystemLogs
    {
        public const string View = "admin.system-logs.view";
        public const string Delete = "admin.system-logs.delete";
    }

    public static class Sms
    {
        public const string ViewDashboard = "admin.sms.view-dashboard";
        public const string SendSingle = "admin.sms.send-single";
        public const string SendBulk = "admin.sms.send-bulk";
        public const string ManageContacts = "admin.sms.manage-contacts";
        public const string ManageGroups = "admin.sms.manage-groups";
        public const string ManageTemplates = "admin.sms.manage-templates";
        public const string ViewHistory = "admin.sms.view-history";
        public const string ExportReports = "admin.sms.export-reports";
        public const string ManageProviders = "admin.sms.manage-providers";
        public const string ManageSettings = "admin.sms.manage-settings";
        public const string CancelCampaign = "admin.sms.cancel-campaign";
    }

    public static readonly IReadOnlyList<AdminPermissionDefinition> Catalog =
    [
        // ── ۱. اصلی ─────────────────────────────────────────────────────────
        new(Dashboard.View, "dashboard", "view", "نمای کلی", "مشاهده داشبورد", "main", "اصلی"),
        new(Dashboard.Update, "dashboard", "update", "نمای کلی", "ویرایش تنظیمات داشبورد", "main", "اصلی"),
        new(CostDashboard.View, "cost-dashboard", "view", "کاست کنترل و قیمت‌گذاری", "مشاهده", "main", "اصلی"),
        new(CostDashboard.Update, "cost-dashboard", "update", "کاست کنترل و قیمت‌گذاری", "ویرایش و ذخیره قیمت‌گذاری", "main", "اصلی"),
        new(Orders.View, "orders", "view", "سفارش‌ها", "مشاهده", "main", "اصلی"),
        new(Orders.Update, "orders", "update", "سفارش‌ها", "ویرایش / آماده‌سازی", "main", "اصلی"),
        new(Orders.Delete, "orders", "delete", "سفارش‌ها", "لغو سفارش", "main", "اصلی"),
        new(Pos.View, "pos", "view", "صندوق فروش (POS)", "مشاهده", "main", "اصلی"),
        new(Pos.Create, "pos", "create", "صندوق فروش (POS)", "ثبت / نگه‌داشت سفارش", "main", "اصلی"),
        new(Pos.Update, "pos", "update", "صندوق فروش (POS)", "ویرایش سفارش", "main", "اصلی"),
        new(Pos.Delete, "pos", "delete", "صندوق فروش (POS)", "حذف سفارش", "main", "اصلی"),
        new(Pos.Pay, "pos", "pay", "صندوق فروش (POS)", "دریافت پرداخت", "main", "اصلی"),
        new(Pos.ApplyManualDiscount, "pos", "apply-discount", "صندوق فروش (POS)", "تخفیف دلخواه", "main", "اصلی"),
        new(Pos.ManagerNote, "pos", "manager-note", "صندوق فروش (POS)", "یادداشت محرمانه مدیریت", "main", "اصلی"),
        new(DeliveryMethods.View, "delivery-methods", "view", "روش‌های تحویل", "مشاهده", "main", "اصلی"),
        new(DeliveryMethods.Create, "delivery-methods", "create", "روش‌های تحویل", "ایجاد", "main", "اصلی"),
        new(DeliveryMethods.Update, "delivery-methods", "update", "روش‌های تحویل", "ویرایش", "main", "اصلی"),
        new(DeliveryMethods.Delete, "delivery-methods", "delete", "روش‌های تحویل", "حذف", "main", "اصلی"),
        new(Customers.View, "customers", "view", "باشگاه مشتریان (گروه‌ها، اشتراک و بج‌ها)", "مشاهده", "main", "اصلی"),
        new(Customers.Create, "customers", "create", "باشگاه مشتریان (گروه‌ها، اشتراک و بج‌ها)", "ایجاد", "main", "اصلی"),
        new(Customers.Update, "customers", "update", "باشگاه مشتریان (گروه‌ها، اشتراک و بج‌ها)", "ویرایش / مدیریت امتیاز", "main", "اصلی"),
        new(Customers.Delete, "customers", "delete", "باشگاه مشتریان (گروه‌ها، اشتراک و بج‌ها)", "حذف", "main", "اصلی"),
        new(Salon.View, "salon", "view", "وضعیت سالن", "مشاهده", "main", "اصلی"),
        new(Salon.Create, "salon", "create", "وضعیت سالن", "ایجاد میز", "main", "اصلی"),
        new(Salon.Update, "salon", "update", "وضعیت سالن", "ویرایش / اشغال / آزادسازی", "main", "اصلی"),
        new(Salon.Delete, "salon", "delete", "وضعیت سالن", "حذف میز", "main", "اصلی"),

        // ── ۲. منو و محصولات ────────────────────────────────────────────────
        new(Categories.View, "categories", "view", "دسته‌بندی‌ها", "مشاهده", "menu", "منو و محصولات"),
        new(Categories.Create, "categories", "create", "دسته‌بندی‌ها", "ایجاد", "menu", "منو و محصولات"),
        new(Categories.Update, "categories", "update", "دسته‌بندی‌ها", "ویرایش", "menu", "منو و محصولات"),
        new(Categories.Delete, "categories", "delete", "دسته‌بندی‌ها", "حذف", "menu", "منو و محصولات"),
        new(MenuItems.View, "menu-items", "view", "آیتم‌های منو", "مشاهده", "menu", "منو و محصولات"),
        new(MenuItems.Create, "menu-items", "create", "آیتم‌های منو", "ایجاد", "menu", "منو و محصولات"),
        new(MenuItems.Update, "menu-items", "update", "آیتم‌های منو", "ویرایش", "menu", "منو و محصولات"),
        new(MenuItems.Delete, "menu-items", "delete", "آیتم‌های منو", "حذف", "menu", "منو و محصولات"),
        new(QuickStock.View, "quick-stock", "view", "موجودی و انقضا سریع", "مشاهده", "menu", "منو و محصولات"),
        new(QuickStock.Update, "quick-stock", "update", "موجودی و انقضا سریع", "ویرایش قیمت، موجودی و پارت انقضا", "menu", "منو و محصولات"),
        new(OptionTemplates.View, "option-templates", "view", "قالب‌های آپشن", "مشاهده", "menu", "منو و محصولات"),
        new(OptionTemplates.Create, "option-templates", "create", "قالب‌های آپشن", "ایجاد", "menu", "منو و محصولات"),
        new(OptionTemplates.Update, "option-templates", "update", "قالب‌های آپشن", "ویرایش", "menu", "منو و محصولات"),
        new(OptionTemplates.Delete, "option-templates", "delete", "قالب‌های آپشن", "حذف", "menu", "منو و محصولات"),
        new(Addons.View, "addons", "view", "افزودنی‌ها", "مشاهده", "menu", "منو و محصولات"),
        new(Addons.Create, "addons", "create", "افزودنی‌ها", "ایجاد", "menu", "منو و محصولات"),
        new(Addons.Update, "addons", "update", "افزودنی‌ها", "ویرایش", "menu", "منو و محصولات"),
        new(Addons.Delete, "addons", "delete", "افزودنی‌ها", "حذف", "menu", "منو و محصولات"),
        new(DrinkBuilder.View, "drink-builder", "view", "سازندهٔ نوشیدنی", "مشاهده", "menu", "منو و محصولات"),
        new(DrinkBuilder.Create, "drink-builder", "create", "سازندهٔ نوشیدنی", "ایجاد", "menu", "منو و محصولات"),
        new(DrinkBuilder.Update, "drink-builder", "update", "سازندهٔ نوشیدنی", "ویرایش", "menu", "منو و محصولات"),
        new(DrinkBuilder.Delete, "drink-builder", "delete", "سازندهٔ نوشیدنی", "حذف", "menu", "منو و محصولات"),
        new(Discounts.View, "discounts", "view", "تخفیف‌ها (گزارش و امنیت)", "مشاهده", "menu", "منو و محصولات"),
        new(Discounts.Create, "discounts", "create", "تخفیف‌ها (گزارش و امنیت)", "ایجاد", "menu", "منو و محصولات"),
        new(Discounts.Update, "discounts", "update", "تخفیف‌ها (گزارش و امنیت)", "ویرایش", "menu", "منو و محصولات"),
        new(Discounts.Delete, "discounts", "delete", "تخفیف‌ها (گزارش و امنیت)", "حذف", "menu", "منو و محصولات"),
        new(Modifiers.View, "modifiers", "view", "تغییرات سفارش", "مشاهده", "menu", "منو و محصولات"),
        new(Modifiers.Create, "modifiers", "create", "تغییرات سفارش", "ایجاد", "menu", "منو و محصولات"),
        new(Modifiers.Update, "modifiers", "update", "تغییرات سفارش", "ویرایش", "menu", "منو و محصولات"),
        new(Modifiers.Delete, "modifiers", "delete", "تغییرات سفارش", "حذف", "menu", "منو و محصولات"),
        new(Blog.View, "blog", "view", "مجله و بلاگ (SEO)", "مشاهده", "menu", "منو و محصولات"),
        new(Blog.Create, "blog", "create", "مجله و بلاگ (SEO)", "ایجاد مقاله / دسته", "menu", "منو و محصولات"),
        new(Blog.Update, "blog", "update", "مجله و بلاگ (SEO)", "ویرایش مقاله / ظاهر", "menu", "منو و محصولات"),
        new(Blog.Delete, "blog", "delete", "مجله و بلاگ (SEO)", "حذف مقاله / دسته", "menu", "منو و محصولات"),

        // ── ۳. آشپزخانه و تولید ─────────────────────────────────────────────
        new(Ingredients.View, "ingredients", "view", "مواد اولیه", "مشاهده", "kitchen", "آشپزخانه و تولید"),
        new(Ingredients.Create, "ingredients", "create", "مواد اولیه", "ایجاد", "kitchen", "آشپزخانه و تولید"),
        new(Ingredients.Update, "ingredients", "update", "مواد اولیه", "ویرایش", "kitchen", "آشپزخانه و تولید"),
        new(Ingredients.Delete, "ingredients", "delete", "مواد اولیه", "حذف", "kitchen", "آشپزخانه و تولید"),
        new(Recipes.View, "recipes", "view", "دستور پخت", "مشاهده", "kitchen", "آشپزخانه و تولید"),
        new(Recipes.Create, "recipes", "create", "دستور پخت", "افزودن خط", "kitchen", "آشپزخانه و تولید"),
        new(Recipes.Update, "recipes", "update", "دستور پخت", "ویرایش", "kitchen", "آشپزخانه و تولید"),
        new(Recipes.Delete, "recipes", "delete", "دستور پخت", "حذف خط", "kitchen", "آشپزخانه و تولید"),
        new(Production.View, "production", "view", "تولید", "مشاهده", "kitchen", "آشپزخانه و تولید"),
        new(Production.Create, "production", "create", "تولید", "ثبت تولید", "kitchen", "آشپزخانه و تولید"),
        new(ProductWiki.View, "product-wiki", "view", "ویکی محصولات", "مشاهده", "kitchen", "آشپزخانه و تولید"),
        new(ProductWiki.Update, "product-wiki", "update", "ویکی محصولات", "ویرایش", "kitchen", "آشپزخانه و تولید"),

        // ── ۴. انبار و تجهیزات ──────────────────────────────────────────────
        new(Warehouses.View, "warehouses", "view", "انبارها", "مشاهده", "warehouse", "انبار و تجهیزات"),
        new(Warehouses.Create, "warehouses", "create", "انبارها", "ایجاد", "warehouse", "انبار و تجهیزات"),
        new(Warehouses.Update, "warehouses", "update", "انبارها", "ویرایش", "warehouse", "انبار و تجهیزات"),
        new(Warehouses.Delete, "warehouses", "delete", "انبارها", "حذف", "warehouse", "انبار و تجهیزات"),
        new(WarehouseVouchers.View, "warehouse-vouchers", "view", "رسید ورود و سند خروج", "مشاهده", "warehouse", "انبار و تجهیزات"),
        new(WarehouseVouchers.Create, "warehouse-vouchers", "create", "رسید ورود و سند خروج", "صدور سند", "warehouse", "انبار و تجهیزات"),
        new(WarehouseVouchers.Update, "warehouse-vouchers", "update", "رسید ورود و سند خروج", "ویرایش / ابطال سند", "warehouse", "انبار و تجهیزات"),
        new(Inventory.View, "inventory", "view", "انبار موجودی", "مشاهده", "warehouse", "انبار و تجهیزات"),
        new(Inventory.Create, "inventory", "create", "انبار موجودی", "ورود / شارژ", "warehouse", "انبار و تجهیزات"),
        new(Inventory.Update, "inventory", "update", "انبار موجودی", "تعدیل / آستانه", "warehouse", "انبار و تجهیزات"),
        new(InventoryPurchases.View, "inventory-purchases", "view", "خرید مواد اولیه", "مشاهده", "warehouse", "انبار و تجهیزات"),
        new(InventoryPurchases.Create, "inventory-purchases", "create", "خرید مواد اولیه", "ثبت خرید", "warehouse", "انبار و تجهیزات"),
        new(Suppliers.View, "suppliers", "view", "تامین‌کنندگان", "مشاهده", "warehouse", "انبار و تجهیزات"),
        new(Suppliers.Create, "suppliers", "create", "تامین‌کنندگان", "ایجاد", "warehouse", "انبار و تجهیزات"),
        new(Suppliers.Update, "suppliers", "update", "تامین‌کنندگان", "ویرایش", "warehouse", "انبار و تجهیزات"),
        new(Suppliers.Delete, "suppliers", "delete", "تامین‌کنندگان", "حذف", "warehouse", "انبار و تجهیزات"),
        new(Assets.View, "assets", "view", "اموال و تجهیزات", "مشاهده", "warehouse", "انبار و تجهیزات"),
        new(Assets.Create, "assets", "create", "اموال و تجهیزات", "ثبت قلم / عکس", "warehouse", "انبار و تجهیزات"),
        new(Assets.Update, "assets", "update", "اموال و تجهیزات", "ویرایش", "warehouse", "انبار و تجهیزات"),
        new(Assets.Delete, "assets", "delete", "اموال و تجهیزات", "حذف", "warehouse", "انبار و تجهیزات"),
        new(Maintenance.View, "maintenance", "view", "سرویس دوره‌ای تجهیزات", "مشاهده", "warehouse", "انبار و تجهیزات"),
        new(Maintenance.Create, "maintenance", "create", "سرویس دوره‌ای تجهیزات", "ایجاد", "warehouse", "انبار و تجهیزات"),
        new(Maintenance.Update, "maintenance", "update", "سرویس دوره‌ای تجهیزات", "ویرایش / ثبت سرویس", "warehouse", "انبار و تجهیزات"),
        new(Maintenance.Delete, "maintenance", "delete", "سرویس دوره‌ای تجهیزات", "حذف", "warehouse", "انبار و تجهیزات"),

        // ── ۵. فروش و مالی ──────────────────────────────────────────────────
        new(RecordSale.View, "record-sale", "view", "ثبت فروش", "مشاهده", "sales", "فروش و مالی"),
        new(RecordSale.Create, "record-sale", "create", "ثبت فروش", "ثبت فروش", "sales", "فروش و مالی"),
        new(Accounting.View, "accounting", "view", "حسابداری", "مشاهده", "sales", "فروش و مالی"),
        new(Accounting.Create, "accounting", "create", "حسابداری", "ثبت سند / سرمایه", "sales", "فروش و مالی"),
        new(Accounting.Update, "accounting", "update", "حسابداری", "ویرایش شرکا / تقسیم سود", "sales", "فروش و مالی"),
        new(Accounting.Delete, "accounting", "delete", "حسابداری", "حذف / لغو", "sales", "فروش و مالی"),
        new(Exports.View, "exports", "view", "خروجی اکسل", "مشاهده و دانلود گزارش اکسل", "sales", "فروش و مالی"),

        // ── ۶. منابع انسانی و پرسنل ─────────────────────────────────────────
        new(Hr.View, "hr", "view", "منابع انسانی", "مشاهده پرسنل و حقوق", "hr", "منابع انسانی و پرسنل"),
        new(Hr.Create, "hr", "create", "منابع انسانی", "ثبت پرسنل / ساختار پرداخت", "hr", "منابع انسانی و پرسنل"),
        new(Hr.Update, "hr", "update", "منابع انسانی", "ویرایش / حضور و پرداخت حقوق", "hr", "منابع انسانی و پرسنل"),
        new(Hr.Delete, "hr", "delete", "منابع انسانی", "حذف / غیرفعال‌سازی", "hr", "منابع انسانی و پرسنل"),
        new(StaffTasks.View, "staff-tasks", "view", "تسک‌های پرسنل", "مشاهده و انجام تسک‌های خود", "hr", "منابع انسانی و پرسنل"),
        new(StaffTasks.ViewAll, "staff-tasks", "view-all", "تسک‌های پرسنل", "مشاهده تسک‌های تمام پرسنل", "hr", "منابع انسانی و پرسنل"),
        new(StaffTasks.Create, "staff-tasks", "create", "تسک‌های پرسنل", "تعریف و تخصیص تسک", "hr", "منابع انسانی و پرسنل"),
        new(StaffTasks.Update, "staff-tasks", "update", "تسک‌های پرسنل", "ویرایش / توقف / بازگشایی", "hr", "منابع انسانی و پرسنل"),
        new(StaffTasks.Delete, "staff-tasks", "delete", "تسک‌های پرسنل", "حذف تسک", "hr", "منابع انسانی و پرسنل"),
        new(StaffTasks.ViewLogs, "staff-tasks", "view-logs", "تسک‌های پرسنل", "مشاهده لاگ و تاخیرها", "hr", "منابع انسانی و پرسنل"),

        // ── ۷. مدیریت پیامک ─────────────────────────────────────────────────
        new(Sms.ViewDashboard, "sms", "view-dashboard", "مدیریت پیامک", "داشبورد و پایش OTP", "sms", "مدیریت پیامک"),
        new(Sms.SendSingle, "sms", "send-single", "مدیریت پیامک", "ارسال تکی و تست OTP", "sms", "مدیریت پیامک"),
        new(Sms.SendBulk, "sms", "send-bulk", "مدیریت پیامک", "ارسال گروهی", "sms", "مدیریت پیامک"),
        new(Sms.ManageContacts, "sms", "manage-contacts", "مدیریت پیامک", "دفترچه مخاطبین", "sms", "مدیریت پیامک"),
        new(Sms.ManageGroups, "sms", "manage-groups", "مدیریت پیامک", "گروه‌های مخاطبین", "sms", "مدیریت پیامک"),
        new(Sms.ManageTemplates, "sms", "manage-templates", "مدیریت پیامک", "قالب‌های پیامکی", "sms", "مدیریت پیامک"),
        new(Sms.ViewHistory, "sms", "view-history", "مدیریت پیامک", "تاریخچه ارسال‌ها", "sms", "مدیریت پیامک"),
        new(Sms.ExportReports, "sms", "export-reports", "مدیریت پیامک", "خروجی گزارش‌ها", "sms", "مدیریت پیامک"),
        new(Sms.ManageProviders, "sms", "manage-providers", "مدیریت پیامک", "سرویس‌دهندگان", "sms", "مدیریت پیامک"),
        new(Sms.ManageSettings, "sms", "manage-settings", "مدیریت پیامک", "تنظیمات پیامک", "sms", "مدیریت پیامک"),
        new(Sms.CancelCampaign, "sms", "cancel-campaign", "مدیریت پیامک", "لغو کمپین گروهی", "sms", "مدیریت پیامک"),

        // ── ۸. سیستم و تنظیمات ──────────────────────────────────────────────
        new(SiteSettings.View, "site-settings", "view", "تنظیمات سایت", "مشاهده", "system", "سیستم و تنظیمات"),
        new(SiteSettings.Update, "site-settings", "update", "تنظیمات سایت", "ویرایش", "system", "سیستم و تنظیمات"),
        new(SpecialRoutes.View, "special-routes", "view", "مسیرهای متفرقه", "مشاهده", "system", "سیستم و تنظیمات"),
        new(SpecialRoutes.Update, "special-routes", "update", "مسیرهای متفرقه", "ویرایش", "system", "سیستم و تنظیمات"),
        new(Tooltips.View, "tooltips", "view", "راهنماهای صفحه‌ها", "مشاهده", "system", "سیستم و تنظیمات"),
        new(Tooltips.Update, "tooltips", "update", "راهنماهای صفحه‌ها", "ویرایش", "system", "سیستم و تنظیمات"),
        new(Users.View, "admin-users", "view", "مدیریت کاربران", "مشاهده", "system", "سیستم و تنظیمات"),
        new(Users.Create, "admin-users", "create", "مدیریت کاربران", "ایجاد کاربر", "system", "سیستم و تنظیمات"),
        new(Users.Update, "admin-users", "update", "مدیریت کاربران", "ویرایش کاربر", "system", "سیستم و تنظیمات"),
        new(Users.Delete, "admin-users", "delete", "مدیریت کاربران", "غیرفعال‌سازی", "system", "سیستم و تنظیمات"),
        new(Users.ManagePermissions, "admin-users", "manage-permissions", "مدیریت کاربران", "تخصیص دسترسی‌ها و نقش‌ها", "system", "سیستم و تنظیمات"),
        new(ActivityLogs.View, "activity-logs", "view", "لاگ فعالیت‌ها", "مشاهده", "system", "سیستم و تنظیمات"),
        new(ActivityLogs.Delete, "activity-logs", "delete", "لاگ فعالیت‌ها", "پاک‌سازی لاگ‌های قدیمی", "system", "سیستم و تنظیمات"),
        new(SystemLogs.View, "system-logs", "view", "لاگ‌های سیستمی", "مشاهده لاگ سرور", "system", "سیستم و تنظیمات"),
        new(SystemLogs.Delete, "system-logs", "delete", "لاگ‌های سیستمی", "حذف فایل لاگ", "system", "سیستم و تنظیمات"),
        new(Uploads.Create, "uploads", "create", "آپلود فایل و تصاویر", "آپلود فایل / تصویر", "system", "سیستم و تنظیمات"),
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
    string ActionLabel,
    string CategoryId = "other",
    string CategoryLabel = "سایر");
