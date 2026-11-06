namespace Sugarista.Application.Discounts.Reports;

/// <summary>
/// Filters + selected columns for a discount performance report. All fields
/// are optional except the period, so the admin can slice by discount, category,
/// kind, channel or a typed code.
/// </summary>
public sealed record DiscountReportFilter(
    DateTimeOffset From,
    DateTimeOffset To,
    Guid? DiscountId = null,
    Guid? CategoryId = null,
    bool OnlyUnscopedCategories = false,
    string? Kind = null,
    string? Channel = null,
    string? Code = null,
    IReadOnlyCollection<string>? Columns = null);

/// <summary>One discount's totals inside the period.</summary>
public sealed record DiscountReportSummaryRow(
    string Name,
    string Kind,
    string? Code,
    decimal Amount,
    int UsageCount,
    int OrderCount,
    int CustomerCount);

/// <summary>Totals per category (null category = whole-order benefits).</summary>
public sealed record DiscountReportCategoryRow(
    Guid? CategoryId,
    string CategoryName,
    decimal Amount,
    int UsageCount,
    int OrderCount);

/// <summary>Totals per local day.</summary>
public sealed record DiscountReportDailyRow(
    string Date,
    decimal Amount,
    int UsageCount,
    int OrderCount);

/// <summary>One discount row (order level) for the detail sheet.</summary>
public sealed record DiscountReportDetailRow(
    string Date,
    string OrderTrackingCode,
    string CustomerName,
    string CustomerPhone,
    string DiscountName,
    string Kind,
    string CategoryName,
    string? Code,
    decimal Amount,
    string Channel,
    string OrderStatus);

/// <summary>Full report payload (preview + Excel source).</summary>
public sealed record DiscountReportResult(
    DateTimeOffset From,
    DateTimeOffset To,
    decimal TotalDiscount,
    int TotalUsage,
    int TotalOrders,
    int TotalCustomers,
    IReadOnlyCollection<DiscountReportSummaryRow> Summary,
    IReadOnlyCollection<DiscountReportCategoryRow> ByCategory,
    IReadOnlyCollection<DiscountReportDailyRow> ByDay,
    IReadOnlyCollection<DiscountReportDetailRow> Details,
    IReadOnlyCollection<DiscountReportColumnOption> Columns);

/// <summary>A selectable report column (key + Persian title).</summary>
public sealed record DiscountReportColumnOption(string Key, string Title);

/// <summary>Column catalogue shared by the admin UI and the Excel builder.</summary>
public static class DiscountReportColumns
{
    public const string DiscountName = "discountName";
    public const string DiscountKind = "discountKind";
    public const string DiscountCode = "discountCode";
    public const string Amount = "amount";
    public const string UsageCount = "usageCount";
    public const string OrderCount = "orderCount";
    public const string CustomerCount = "customerCount";
    public const string Category = "category";
    public const string Date = "date";
    public const string OrderTrackingCode = "orderTrackingCode";
    public const string CustomerName = "customerName";
    public const string CustomerPhone = "customerPhone";
    public const string Channel = "channel";
    public const string OrderStatus = "orderStatus";

    public static readonly IReadOnlyList<DiscountReportColumnOption> All =
    [
        new(DiscountName, "نام تخفیف"),
        new(DiscountKind, "نوع تخفیف"),
        new(DiscountCode, "کد تخفیف"),
        new(Amount, "مبلغ تخفیف (تومان)"),
        new(UsageCount, "تعداد استفاده"),
        new(OrderCount, "تعداد سفارش"),
        new(CustomerCount, "تعداد مشتری"),
        new(Category, "دسته‌بندی"),
        new(Date, "تاریخ"),
        new(OrderTrackingCode, "کد رهگیری سفارش"),
        new(CustomerName, "نام مشتری"),
        new(CustomerPhone, "شماره مشتری"),
        new(Channel, "کانال فروش"),
        new(OrderStatus, "وضعیت سفارش"),
    ];

    public static readonly IReadOnlySet<string> KnownKeys =
        All.Select(column => column.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);

    public static string TitleOf(string key) =>
        All.FirstOrDefault(column => string.Equals(column.Key, key, StringComparison.OrdinalIgnoreCase))?.Title
        ?? key;

    public static IReadOnlyList<string> Normalize(IReadOnlyCollection<string>? columns)
    {
        if (columns is null || columns.Count == 0)
        {
            return All.Select(column => column.Key).ToList();
        }

        return columns
            .Where(column => KnownKeys.Contains(column))
            .Select(column => All.First(entry => string.Equals(entry.Key, column, StringComparison.OrdinalIgnoreCase)).Key)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
