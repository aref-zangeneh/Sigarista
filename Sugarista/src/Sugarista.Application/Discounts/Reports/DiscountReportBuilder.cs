using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Discounts.Reports;

/// <summary>
/// Builds the discount performance report from <see cref="OrderDiscount"/> rows.
/// Only realized (paid / partially paid) orders are counted, so cancelled or
/// failed orders never inflate the numbers. The same data powers the on-screen
/// preview, the Excel download and the scheduled e-mail report.
/// </summary>
public sealed class DiscountReportBuilder
{
    private const int MaxDetailRows = 50000;

    private readonly IApplicationDbContext _dbContext;
    private readonly IExcelExporter _excelExporter;

    public DiscountReportBuilder(IApplicationDbContext dbContext, IExcelExporter excelExporter)
    {
        _dbContext = dbContext;
        _excelExporter = excelExporter;
    }

    public async Task<DiscountReportResult> BuildAsync(
        DiscountReportFilter filter,
        CancellationToken cancellationToken = default)
    {
        if (filter.To <= filter.From)
        {
            throw new Domain.Exceptions.DomainException("بازهٔ گزارش معتبر نیست؛ تاریخ پایان باید بعد از تاریخ شروع باشد.");
        }

        // PostgreSQL timestamptz only accepts UTC parameters. The admin UI sends
        // Tehran-local offsets, so normalize the range before it reaches Npgsql.
        var fromUtc = filter.From.ToUniversalTime();
        var toUtc = filter.To.ToUniversalTime();

        var kindFilter = string.IsNullOrWhiteSpace(filter.Kind)
            ? null
            : filter.Kind.Trim();
        var channelFilter = string.IsNullOrWhiteSpace(filter.Channel)
            ? null
            : filter.Channel.Trim();
        var codeFilter = Discount.NormalizeCode(filter.Code);

        var query = _dbContext.OrderDiscounts
            .AsNoTracking()
            .Where(discount => discount.RedeemedAt >= fromUtc && discount.RedeemedAt < toUtc)
            .Join(
                _dbContext.Orders.AsNoTracking(),
                discount => discount.OrderId,
                order => order.Id,
                (discount, order) => new { Discount = discount, Order = order })
            .Where(entry => entry.Order.Status == OrderStatus.Paid ||
                            entry.Order.Status == OrderStatus.PartiallyPaid);

        if (filter.DiscountId is { } discountId)
        {
            query = query.Where(entry => entry.Discount.DiscountId == discountId);
        }

        if (filter.CategoryId is { } categoryId)
        {
            query = query.Where(entry => entry.Discount.CategoryId == categoryId);
        }
        else if (filter.OnlyUnscopedCategories)
        {
            query = query.Where(entry => entry.Discount.CategoryId == null);
        }

        if (kindFilter is not null)
        {
            if (!Enum.TryParse<OrderDiscountKind>(kindFilter, true, out var kindValue))
            {
                throw new Domain.Exceptions.DomainException("نوع تخفیف انتخاب‌شده معتبر نیست.");
            }

            query = query.Where(entry => entry.Discount.Kind == kindValue);
        }

        if (channelFilter is not null)
        {
            if (!Enum.TryParse<OrderChannel>(channelFilter, true, out var channelValue))
            {
                throw new Domain.Exceptions.DomainException("کانال فروش انتخاب‌شده معتبر نیست.");
            }

            query = query.Where(entry => entry.Order.Channel == channelValue);
        }

        if (codeFilter is not null)
        {
            query = query.Where(entry => entry.Discount.Code == codeFilter);
        }

        var rawRows = await query
            .OrderByDescending(entry => entry.Discount.RedeemedAt)
            .Select(entry => new
            {
                entry.Discount.DiscountId,
                entry.Discount.Name,
                entry.Discount.Kind,
                entry.Discount.Code,
                entry.Discount.Amount,
                entry.Discount.CategoryId,
                entry.Discount.CustomerId,
                entry.Discount.RedeemedAt,
                entry.Order.Id,
                entry.Order.TrackingCode,
                entry.Order.CustomerName,
                entry.Order.CustomerPhone,
                entry.Order.Channel,
                entry.Order.Status,
            })
            .Take(MaxDetailRows + 1)
            .ToListAsync(cancellationToken);

        var truncated = rawRows.Count > MaxDetailRows;
        if (truncated)
        {
            rawRows = rawRows.Take(MaxDetailRows).ToList();
        }

        var rows = rawRows
            .Select(row => new RawRow(
                row.DiscountId,
                row.Name,
                row.Kind,
                row.Code,
                row.Amount,
                row.CategoryId,
                row.CustomerId,
                row.RedeemedAt,
                row.Id,
                row.TrackingCode,
                row.CustomerName,
                row.CustomerPhone,
                row.Channel,
                row.Status))
            .ToList();

        var categoryNames = await _dbContext.Categories
            .AsNoTracking()
            .Select(category => new { category.Id, category.Name })
            .ToDictionaryAsync(category => category.Id, category => category.Name, cancellationToken);

        var timeZone = ResolveTimeZone(filter.From.Offset);

        string CategoryNameOf(Guid? categoryId) =>
            categoryId is { } id && categoryNames.TryGetValue(id, out var name)
                ? name
                : "کل سفارش";

        var summary = rows
            .GroupBy(row => new { row.DiscountId, row.Name, row.Kind, row.Code })
            .Select(group => new DiscountReportSummaryRow(
                group.Key.Name,
                group.Key.Kind.ToString(),
                group.Key.Code,
                group.Sum(row => row.Amount),
                group.Count(),
                group.Select(row => row.OrderId).Distinct().Count(),
                group.Select(row => row.CustomerId).Where(id => id is not null).Distinct().Count()))
            .OrderByDescending(row => row.Amount)
            .ToList();

        var byCategory = rows
            .GroupBy(row => row.CategoryId)
            .Select(group => new DiscountReportCategoryRow(
                group.Key,
                CategoryNameOf(group.Key),
                group.Sum(row => row.Amount),
                group.Count(),
                group.Select(row => row.OrderId).Distinct().Count()))
            .OrderByDescending(row => row.Amount)
            .ToList();

        var byDay = rows
            .GroupBy(row => TimeZoneInfo.ConvertTime(row.RedeemedAt, timeZone).Date)
            .Select(group => new DiscountReportDailyRow(
                group.Key.ToString("yyyy-MM-dd"),
                group.Sum(row => row.Amount),
                group.Count(),
                group.Select(row => row.OrderId).Distinct().Count()))
            .OrderByDescending(row => row.Date)
            .ToList();

        var details = rows
            .Select(row => new DiscountReportDetailRow(
                TimeZoneInfo.ConvertTime(row.RedeemedAt, timeZone).ToString("yyyy-MM-dd HH:mm"),
                row.TrackingCode,
                row.CustomerName,
                row.CustomerPhone,
                row.Name,
                row.Kind.ToString(),
                CategoryNameOf(row.CategoryId),
                row.Code,
                row.Amount,
                row.Channel.ToString(),
                row.Status.ToString()))
            .ToList();

        return new DiscountReportResult(
            filter.From,
            filter.To,
            rows.Sum(row => row.Amount),
            rows.Count,
            rows.Select(row => row.OrderId).Distinct().Count(),
            rows.Select(row => row.CustomerId).Where(id => id is not null).Distinct().Count(),
            summary,
            byCategory,
            byDay,
            details,
            DiscountReportColumns.Normalize(filter.Columns)
                .Select(key => DiscountReportColumns.All.First(column => column.Key == key))
                .ToList());
    }

    /// <summary>Builds the styled Excel workbook for the selected columns.</summary>
    public async Task<ExcelWorkbookFile> BuildExcelAsync(
        DiscountReportFilter filter,
        CancellationToken cancellationToken = default)
    {
        var report = await BuildAsync(filter, cancellationToken);
        var columns = report.Columns.Select(column => column.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var sheets = new List<ExcelSheet>();

        AddSheet(
            sheets,
            "خلاصه تخفیف‌ها",
            [
                (DiscountReportColumns.DiscountName, "نام تخفیف"),
                (DiscountReportColumns.DiscountKind, "نوع تخفیف"),
                (DiscountReportColumns.DiscountCode, "کد تخفیف"),
                (DiscountReportColumns.Amount, "مبلغ تخفیف (تومان)"),
                (DiscountReportColumns.UsageCount, "تعداد استفاده"),
                (DiscountReportColumns.OrderCount, "تعداد سفارش"),
                (DiscountReportColumns.CustomerCount, "تعداد مشتری"),
            ],
            columns,
            report.Summary.Select(row => (IReadOnlyList<object?>)
            [
                row.Name,
                KindTitle(row.Kind),
                row.Code,
                row.Amount,
                row.UsageCount,
                row.OrderCount,
                row.CustomerCount,
            ]).ToList());

        AddSheet(
            sheets,
            "تفکیک دسته‌بندی",
            [
                (DiscountReportColumns.Category, "دسته‌بندی"),
                (DiscountReportColumns.Amount, "مبلغ تخفیف (تومان)"),
                (DiscountReportColumns.UsageCount, "تعداد استفاده"),
                (DiscountReportColumns.OrderCount, "تعداد سفارش"),
            ],
            columns,
            report.ByCategory.Select(row => (IReadOnlyList<object?>)
            [
                row.CategoryName,
                row.Amount,
                row.UsageCount,
                row.OrderCount,
            ]).ToList());

        AddSheet(
            sheets,
            "تفکیک روزانه",
            [
                (DiscountReportColumns.Date, "تاریخ"),
                (DiscountReportColumns.Amount, "مبلغ تخفیف (تومان)"),
                (DiscountReportColumns.UsageCount, "تعداد استفاده"),
                (DiscountReportColumns.OrderCount, "تعداد سفارش"),
            ],
            columns,
            report.ByDay.Select(row => (IReadOnlyList<object?>)
            [
                row.Date,
                row.Amount,
                row.UsageCount,
                row.OrderCount,
            ]).ToList());

        AddSheet(
            sheets,
            "جزئیات",
            [
                (DiscountReportColumns.Date, "تاریخ"),
                (DiscountReportColumns.OrderTrackingCode, "کد رهگیری سفارش"),
                (DiscountReportColumns.CustomerName, "نام مشتری"),
                (DiscountReportColumns.CustomerPhone, "شماره مشتری"),
                (DiscountReportColumns.DiscountName, "نام تخفیف"),
                (DiscountReportColumns.DiscountKind, "نوع تخفیف"),
                (DiscountReportColumns.Category, "دسته‌بندی"),
                (DiscountReportColumns.DiscountCode, "کد تخفیف"),
                (DiscountReportColumns.Amount, "مبلغ تخفیف (تومان)"),
                (DiscountReportColumns.Channel, "کانال فروش"),
                (DiscountReportColumns.OrderStatus, "وضعیت سفارش"),
            ],
            columns,
            report.Details.Select(row => (IReadOnlyList<object?>)
            [
                row.Date,
                row.OrderTrackingCode,
                row.CustomerName,
                row.CustomerPhone,
                row.DiscountName,
                KindTitle(row.Kind),
                row.CategoryName,
                row.Code,
                row.Amount,
                ChannelTitle(row.Channel),
                StatusTitle(row.OrderStatus),
            ]).ToList());

        if (sheets.Count == 0)
        {
            throw new Domain.Exceptions.DomainException("حداقل یک ستون برای گزارش انتخاب کنید.");
        }

        var title = $"گزارش عملکرد تخفیف‌ها — {report.From:yyyy-MM-dd} تا {report.To:yyyy-MM-dd}";
        return _excelExporter.BuildWorkbook(title, sheets);
    }

    private static void AddSheet(
        ICollection<ExcelSheet> sheets,
        string name,
        IReadOnlyList<(string Key, string Title)> availableColumns,
        IReadOnlySet<string> selectedColumns,
        IReadOnlyList<IReadOnlyList<object?>> rows)
    {
        var indexes = availableColumns
            .Select((column, index) => (column, index))
            .Where(entry => selectedColumns.Contains(entry.column.Key))
            .ToList();

        if (indexes.Count == 0)
        {
            return;
        }

        var headers = indexes.Select(entry => entry.column.Title).ToList();
        var filteredRows = rows
            .Select(row => (IReadOnlyList<object?>)indexes.Select(entry => row[entry.index]).ToList())
            .ToList();

        sheets.Add(new ExcelSheet(name, headers, filteredRows));
    }

    public static string KindTitle(string kind) => kind switch
    {
        nameof(OrderDiscountKind.Code) => "کد تخفیف",
        nameof(OrderDiscountKind.Tier) => "سطح عضویت",
        nameof(OrderDiscountKind.Group) => "گروه مشتری",
        nameof(OrderDiscountKind.Birthday) => "تولد",
        nameof(OrderDiscountKind.Cup) => "لیوان شخصی",
        nameof(OrderDiscountKind.Subscription) => "اشتراک ماهانه",
        nameof(OrderDiscountKind.LoyaltyPoints) => "امتیاز باشگاه",
        nameof(OrderDiscountKind.StampCard) => "کارت مهر",
        nameof(OrderDiscountKind.Referral) => "معرفی دوستان",
        _ => kind,
    };

    public static string ChannelTitle(string channel) => channel switch
    {
        "Online" => "آنلاین",
        "Pos" => "حضوری (POS)",
        _ => channel,
    };

    public static string StatusTitle(string status) => status switch
    {
        "Paid" => "پرداخت‌شده",
        "PartiallyPaid" => "پرداخت جزئی",
        _ => status,
    };

    private static TimeZoneInfo ResolveTimeZone(TimeSpan offset)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.CreateCustomTimeZone("report", offset, "report", "report");
        }
    }

    private sealed record RawRow(
        Guid? DiscountId,
        string Name,
        OrderDiscountKind Kind,
        string? Code,
        decimal Amount,
        Guid? CategoryId,
        Guid? CustomerId,
        DateTimeOffset RedeemedAt,
        Guid OrderId,
        string TrackingCode,
        string CustomerName,
        string CustomerPhone,
        OrderChannel Channel,
        OrderStatus Status);
}
