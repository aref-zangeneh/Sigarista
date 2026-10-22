namespace Sugarista.Application.ActivityLogs;

public static class ActivityActions
{
    public const string Login = "login";
    public const string Logout = "logout";
    public const string Create = "create";
    public const string Update = "update";
    public const string Delete = "delete";
    public const string Status = "status";
    public const string Upload = "upload";
    public const string Export = "export";
    public const string Other = "other";

    public static string TitleOf(string action) => action switch
    {
        Login => "ورود",
        Logout => "خروج",
        Create => "ایجاد",
        Update => "ویرایش",
        Delete => "حذف",
        Status => "تغییر وضعیت",
        Upload => "بارگذاری فایل",
        Export => "دریافت خروجی",
        _ => "عملیات",
    };

    public static IReadOnlyList<string> All { get; } =
    [
        Login, Logout, Create, Update, Delete, Status, Upload, Export, Other,
    ];
}

/// <summary>
/// Turns raw request facts into the finished Persian sentence stored in the log,
/// e.g. «آرمین در بخش «سفارشات» سفارش #A1234 را ویرایش کرد — فیلدهای تغییر‌یافته: قیمت، محصول».
/// </summary>
public static class ActivityLogNarrator
{
    public static string Describe(
        string actor,
        string sectionTitle,
        string action,
        string? entityLabel,
        IReadOnlyList<string> changedFields,
        bool succeeded,
        int statusCode)
    {
        if (action == ActivityActions.Login)
        {
            return succeeded
                ? $"«{actor}» وارد پنل مدیریت شد."
                : $"«{actor}» تلاش کرد وارد پنل مدیریت شود — ناموفق (کد {statusCode}).";
        }

        if (action == ActivityActions.Logout)
        {
            return succeeded
                ? $"«{actor}» از پنل مدیریت خارج شد."
                : $"«{actor}» از پنل مدیریت خارج شد (با خطا — کد {statusCode}).";
        }

        var labelPhrase = string.IsNullOrWhiteSpace(entityLabel) ? "یک مورد" : entityLabel;

        if (!succeeded)
        {
            return BuildFailureSentence(actor, sectionTitle, action, labelPhrase, statusCode);
        }

        var fieldsPhrase = changedFields.Count == 0
            ? string.Empty
            : $" — فیلدهای تغییر‌یافته: {string.Join("، ", changedFields.Take(8))}";

        return action switch
        {
            ActivityActions.Create when string.IsNullOrWhiteSpace(entityLabel) =>
                $"«{actor}» در بخش «{sectionTitle}» مورد تازه‌ای ثبت کرد.",
            ActivityActions.Create =>
                $"«{actor}» در بخش «{sectionTitle}» مورد تازه‌ای ثبت کرد: {labelPhrase}.",
            ActivityActions.Update =>
                $"«{actor}» در بخش «{sectionTitle}» {labelPhrase} را ویرایش کرد{fieldsPhrase}.",
            ActivityActions.Delete =>
                $"«{actor}» در بخش «{sectionTitle}» {labelPhrase} را حذف کرد.",
            ActivityActions.Status =>
                $"«{actor}» در بخش «{sectionTitle}» وضعیت {labelPhrase} را تغییر داد{fieldsPhrase}.",
            ActivityActions.Upload =>
                $"«{actor}» در بخش «{sectionTitle}» فایل یا تصویر جدیدی بارگذاری کرد.",
            ActivityActions.Export =>
                $"«{actor}» از بخش «{sectionTitle}» خروجی گرفت.",
            _ =>
                $"«{actor}» در بخش «{sectionTitle}» عملیاتی انجام داد.",
        };
    }

    private static string BuildFailureSentence(
        string actor,
        string sectionTitle,
        string action,
        string labelPhrase,
        int statusCode)
    {
        var attempt = action switch
        {
            ActivityActions.Create => "موردی ثبت کند",
            ActivityActions.Update => $"{labelPhrase} را ویرایش کند",
            ActivityActions.Delete => $"{labelPhrase} را حذف کند",
            ActivityActions.Status => $"وضعیت {labelPhrase} را تغییر دهد",
            ActivityActions.Upload => "فایلی بارگذاری کند",
            ActivityActions.Export => "خروجی بگیرد",
            _ => "عملیاتی انجام دهد",
        };

        return $"«{actor}» تلاش کرد در بخش «{sectionTitle}» {attempt} — ناموفق (کد {statusCode}).";
    }
}
