namespace Sugarista.Application.ActivityLogs;

/// <summary>
/// Central map between API route segments and the Persian section titles used
/// both in the activity log sentences and in the log page filters.
/// </summary>
public static class AdminSections
{
    public const string UnknownSectionId = "other";

    private static readonly Dictionary<string, string> Titles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["dashboard"] = "داشبورد",
        ["cost-dashboard"] = "داشبورد هزینه",
        ["salon"] = "سالن",
        ["categories"] = "دسته‌بندی‌ها",
        ["menu-items"] = "آیتم‌های منو",
        ["addons"] = "افزودنی‌ها",
        ["modifiers"] = "تغییرات سفارش",
        ["ingredients"] = "مواد اولیه",
        ["suppliers"] = "تأمین‌کنندگان",
        ["recipes"] = "دستور پخت",
        ["product-wiki"] = "ویکی محصولات",
        ["inventory"] = "انبار موجودی",
        ["inventory-purchases"] = "خرید مواد",
        ["production"] = "تولید",
        ["sales"] = "ثبت فروش",
        ["record-sale"] = "ثبت فروش",
        ["orders"] = "سفارشات",
        ["pos"] = "صندوق فروش",
        ["customers"] = "مشتریان",
        ["delivery-methods"] = "روش‌های ارسال",
        ["accounting"] = "حسابداری",
        ["exports"] = "خروجی گزارش‌ها",
        ["analytics"] = "تحلیل و آمار",
        ["hr"] = "منابع انسانی",
        ["assets"] = "اموال و تجهیزات",
        ["site-settings"] = "تنظیمات سایت",
        ["users"] = "مدیریت کاربران",
        ["roles"] = "نقش‌ها",
        ["uploads"] = "فایل‌ها",
        ["auth"] = "ورود و خروج",
        ["activity-logs"] = "لاگ فعالیت‌ها",
        ["files"] = "فایل‌ها",
    };

    /// <summary>Resolves the section of a request path such as /api/admin/orders/5.</summary>
    public static (string SectionId, string SectionTitle) Resolve(string path)
    {
        var segments = path
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Where(segment => !string.Equals(segment, "api", StringComparison.OrdinalIgnoreCase))
            .ToList();

        // drop the leading "admin"
        if (segments.Count > 0 && string.Equals(segments[0], "admin", StringComparison.OrdinalIgnoreCase))
        {
            segments.RemoveAt(0);
        }

        if (segments.Count == 0)
        {
            return (UnknownSectionId, "سایر بخش‌ها");
        }

        var sectionId = segments[0].ToLowerInvariant();

        if (sectionId == "inventory" && segments.Count > 1 &&
            string.Equals(segments[1], "purchases", StringComparison.OrdinalIgnoreCase))
        {
            sectionId = "inventory-purchases";
        }
        else if (sectionId == "users" && segments.Count > 1 &&
                 string.Equals(segments[1], "roles", StringComparison.OrdinalIgnoreCase))
        {
            sectionId = "roles";
        }

        return Titles.TryGetValue(sectionId, out var title)
            ? (sectionId, title)
            : (sectionId, sectionId);
    }

    public static string TitleOf(string sectionId) =>
        Titles.TryGetValue(sectionId, out var title) ? title : sectionId;

    /// <summary>Persian noun used in sentences such as «سفارش #A1B2C3D4 را ویرایش کرد».</summary>
    public static string EntityNounOf(string sectionId) => sectionId switch
    {
        "orders" => "سفارش",
        "pos" => "سفارش",
        "sales" => "فروش",
        "menu-items" => "آیتم منو",
        "categories" => "دسته",
        "addons" => "افزودنی",
        "modifiers" => "تغییر سفارش",
        "ingredients" => "ماده اولیه",
        "inventory" => "ماده اولیه",
        "inventory-purchases" => "خرید",
        "suppliers" => "تأمین‌کننده",
        "recipes" => "دستور پخت",
        "product-wiki" => "محصول",
        "production" => "تولید",
        "customers" => "مشتری",
        "delivery-methods" => "روش ارسال",
        "accounting" => "سند",
        "hr" => "پرونده پرسنلی",
        "assets" => "قلم اموال",
        "users" => "کاربر",
        "roles" => "نقش",
        "site-settings" => "تنظیمات",
        "salon" => "میز",
        "uploads" => "فایل",
        "activity-logs" => "لاگ",
        "auth" => "ورود",
        _ => "مورد",
    };

    public static IReadOnlyDictionary<string, string> AllTitles => Titles;
}
