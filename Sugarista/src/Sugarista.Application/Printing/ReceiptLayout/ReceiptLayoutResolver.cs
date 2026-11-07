using Sugarista.Domain.Exceptions;

namespace Sugarista.Application.Printing.ReceiptLayout;

/// <summary>
/// Merges a partial layout document over the built-in defaults, normalizes it
/// and validates every piece — producing the resolved document stored on
/// <c>SiteSettings.ReceiptLayoutJson</c> and embedded in print payloads.
/// </summary>
public static class ReceiptLayoutResolver
{
    /// <summary>Merges the patch over the defaults, normalizes and validates it.</summary>
    /// <exception cref="DomainException">When any value is invalid (Persian message).</exception>
    public static ResolvedReceiptLayout Resolve(ReceiptLayoutPatch? patch)
    {
        var errors = new List<string>();

        var customer = MergeCustomer(patch?.Customer ?? new CustomerReceiptPatch(null, null, null, null, null, null, null), errors);
        var barista = MergeBarista(patch?.Barista ?? new BaristaReceiptPatch(null, null, null, null, null, null), errors);

        if (errors.Count > 0)
        {
            throw new DomainException(string.Join(' ', errors));
        }

        return new ResolvedReceiptLayout(customer, barista);
    }

    // ── customer receipt ─────────────────────────────────────────────────────

    private static CustomerReceiptLayout MergeCustomer(CustomerReceiptPatch patch, List<string> errors)
    {
        var defaults = ReceiptLayoutDefault.Customer();
        var header = patch.Header ?? new CustomerHeaderPatch(null, null);

        var headerRows = FilterKnownKeys(
            patch.HeaderRows,
            ReceiptLayoutKeys.CustomerHeaderRows,
            "سطرهای اطلاعات فیش مشتری",
            errors);

        var paymentRows = FilterKnownKeys(
            patch.PaymentRows,
            ReceiptLayoutKeys.CustomerPaymentRows,
            "سطرهای پرداخت فیش مشتری",
            errors);

        if (patch.Footer?.Text is { Length: > 300 })
        {
            errors.Add("متن پانویس فیش مشتری حداکثر می‌تواند ۳۰۰ نویسه باشد.");
        }

        return new CustomerReceiptLayout(
            Header: new CustomerHeaderLayout(
                header.ShowAddress ?? defaults.Header.ShowAddress,
                header.ShowPhone ?? defaults.Header.ShowPhone),
            HeaderRows: headerRows,
            Items: MergeItems(
                patch.Items, defaults.Items,
                ReceiptLayoutKeys.CustomerSubLines, ReceiptLayoutKeys.ItemColumnKeys,
                "فیش مشتری", errors, allowMoneyColumns: true),
            Totals: MergeTotals(patch.Totals, defaults.Totals, errors),
            PaymentRows: paymentRows,
            Footer: patch.Footer is null
                ? defaults.Footer
                : new CustomerFooterLayout(
                    patch.Footer.Text ?? defaults.Footer.Text,
                    patch.Footer.ShowThanks ?? defaults.Footer.ShowThanks,
                    patch.Footer.ShowCafeName ?? defaults.Footer.ShowCafeName),
            Fonts: MergeFonts(patch.Fonts, defaults.Fonts, errors));
    }

    // ── barista ticket ───────────────────────────────────────────────────────

    private static BaristaReceiptLayout MergeBarista(BaristaReceiptPatch patch, List<string> errors)
    {
        var defaults = ReceiptLayoutDefault.Barista();

        var title = patch.Title ?? defaults.Title;
        if (string.IsNullOrWhiteSpace(title) || title.Length > 60)
        {
            errors.Add("عنوان فیش آشپزخانه باید بین ۱ تا ۶۰ نویسه باشد.");
        }

        if (patch.Footer?.Text is { Length: > 300 })
        {
            errors.Add("متن پانویس فیش آشپزخانه حداکثر می‌تواند ۳۰۰ نویسه باشد.");
        }

        var infoRows = patch.InfoRows is null
            ? [.. defaults.InfoRows]
            : FilterKnownKeys(patch.InfoRows, ReceiptLayoutKeys.BaristaInfoRows, "سطرهای اطلاعات فیش آشپزخانه", errors);

        // The kitchen ticket never prints money — reject money columns up front
        // with a dedicated message before the generic unknown-key one.
        if (patch.Items?.Columns?.Any(column =>
                (column.Key ?? string.Empty).Trim()
                is ReceiptLayoutKeys.ColumnUnitPrice or ReceiptLayoutKeys.ColumnTotal) == true)
        {
            errors.Add("فیش آشپزخانه ستون مبلغی ندارد؛ ستون «قیمت واحد» یا «جمع» روی آن معنا ندارد.");
        }

        return new BaristaReceiptLayout(
            Title: title,
            InfoRows: infoRows,
            Items: MergeItems(
                patch.Items, defaults.Items,
                ReceiptLayoutKeys.BaristaSubLines,
                [ReceiptLayoutKeys.ColumnName, ReceiptLayoutKeys.ColumnQty],
                "فیش آشپزخانه", errors, allowMoneyColumns: false),
            ShowOrderNote: patch.ShowOrderNote ?? defaults.ShowOrderNote,
            Footer: patch.Footer is null
                ? defaults.Footer
                : new BaristaFooterLayout(
                    patch.Footer.Text ?? defaults.Footer.Text,
                    patch.Footer.ShowSign ?? defaults.Footer.ShowSign),
            Fonts: MergeBaristaFonts(patch.Fonts, defaults.Fonts, errors));
    }

    // ── items table ──────────────────────────────────────────────────────────

    private static ReceiptItemsLayout MergeItems(
        ReceiptItemsPatch? patch,
        ReceiptItemsLayout defaults,
        IReadOnlyList<string> allowedSubLines,
        IReadOnlyList<string> allowedColumnKeys,
        string copyName,
        List<string> errors,
        bool allowMoneyColumns)
    {
        patch ??= new ReceiptItemsPatch(null, null, null, null, null, null);

        var columns = ResolveColumns(patch.Columns, defaults.Columns, allowedColumnKeys, copyName, errors, allowMoneyColumns);

        if (!columns.Any(column => column.Key == ReceiptLayoutKeys.ColumnName && column.Visible))
        {
            errors.Add($"ستون «نام آیتم» در {copyName} نمی‌تواند حذف شود.");
        }

        if (allowMoneyColumns && !columns.Any(column => column.Key == ReceiptLayoutKeys.ColumnTotal && column.Visible))
        {
            errors.Add($"ستون «جمع» در {copyName} نمی‌تواند حذف شود.");
        }

        var subLines = FilterKnownKeys(patch.SubLines, allowedSubLines, $"سطرهای جزئیات آیتم در {copyName}", errors);

        return new ReceiptItemsLayout(
            Columns: columns,
            SubLines: subLines,
            ShowHeaderRow: patch.ShowHeaderRow ?? defaults.ShowHeaderRow,
            ShowOptionPrices: patch.ShowOptionPrices ?? defaults.ShowOptionPrices,
            ShowUnitPriceNote: patch.ShowUnitPriceNote ?? defaults.ShowUnitPriceNote,
            FontSize: ValidateFont(patch.FontSize, defaults.FontSize, $"اندازه فونت آیتم‌های {copyName}", errors));
    }

    private static List<ReceiptColumnLayout> ResolveColumns(
        List<ReceiptColumnPatch>? posted,
        List<ReceiptColumnLayout> defaults,
        IReadOnlyList<string> allowedKeys,
        string copyName,
        List<string> errors,
        bool allowMoneyColumns)
    {
        if (posted is null)
        {
            return [.. defaults];
        }

        var seen = new List<string>();
        var columns = new List<ReceiptColumnLayout>();

        foreach (var column in posted)
        {
            var key = (column.Key ?? string.Empty).Trim();
            if (!allowedKeys.Contains(key))
            {
                errors.Add($"ستون «{key}» در {copyName} شناسهٔ مجازی ندارد؛ شناسه‌های مجاز: {string.Join("، ", allowedKeys)}.");
                continue;
            }

            if (seen.Contains(key))
            {
                errors.Add($"ستون «{key}» در {copyName} تکراری است.");
                continue;
            }

            seen.Add(key);

            var defaultColumn = defaults.First(candidate => candidate.Key == key);

            int? widthPct = column.WidthPct;
            if (widthPct.HasValue)
            {
                var fixedWidthAllowed = allowMoneyColumns
                    && ReceiptLayoutKeys.TableBandColumnKeys.Contains(key);
                if (!fixedWidthAllowed
                    || widthPct < ReceiptLayoutKeys.MinWidthPct
                    || widthPct > ReceiptLayoutKeys.MaxWidthPct)
                {
                    if (fixedWidthAllowed)
                    {
                        errors.Add($"عرض ستون «{key}» باید عددی بین {ReceiptLayoutKeys.MinWidthPct} تا {ReceiptLayoutKeys.MaxWidthPct} درصد باشد.");
                    }
                    else
                    {
                        errors.Add("ستون‌های عرض ثابت فقط برای ستون‌های مبلغی (قیمت واحد یا جمع) مجاز هستند.");
                    }

                    widthPct = null;
                }
            }

            columns.Add(new ReceiptColumnLayout(
                Key: key,
                Label: ValidateLabel(column.Label, defaultColumn.Label, $"عنوان ستون «{key}» در {copyName}", errors),
                Visible: column.Visible ?? defaultColumn.Visible,
                WidthPct: widthPct));
        }

        // Columns the admin did not mention keep their default position value,
        // appended at the end (order among posted ones is preserved).
        columns.AddRange(defaults.Where(defaultColumn => !seen.Contains(defaultColumn.Key)));

        return columns;
    }

    // ── totals ───────────────────────────────────────────────────────────────

    private static ReceiptTotalsLayout MergeTotals(ReceiptTotalsPatch? patch, ReceiptTotalsLayout defaults, List<string> errors)
    {
        if (patch?.Rows is null)
        {
            return new ReceiptTotalsLayout([.. defaults.Rows]);
        }

        var seen = new Dictionary<string, ReceiptTotalRowLayout>();
        foreach (var row in patch.Rows)
        {
            var key = (row.Key ?? string.Empty).Trim();
            if (!ReceiptLayoutKeys.TotalRowKeys.Contains(key))
            {
                errors.Add($"«{key}» شناسهٔ مجاز ردیف‌های جمع نیست؛ شناسه‌های مجاز: {string.Join("، ", ReceiptLayoutKeys.TotalRowKeys)}.");
                continue;
            }

            if (seen.ContainsKey(key))
            {
                errors.Add($"ردیف «{key}» در جمع فیش تکراری است.");
                continue;
            }

            var defaultRow = defaults.Rows.First(candidate => candidate.Key == key);
            seen[key] = new ReceiptTotalRowLayout(
                Key: key,
                Label: ValidateLabel(row.Label, defaultRow.Label, $"عنوان ردیف «{key}»", errors),
                Visible: row.Visible ?? defaultRow.Visible);
        }

        var totalVisible = seen.TryGetValue(ReceiptLayoutKeys.TotalRowTotal, out var totalRow)
            ? totalRow.Visible
            : defaults.Rows.First(row => row.Key == ReceiptLayoutKeys.TotalRowTotal).Visible;
        if (!totalVisible)
        {
            errors.Add("ردیف «مبلغ نهایی» نمی‌تواند از فیش حذف شود.");
        }

        // Canonical totals order: subtotal → deliveryFee → tax → total.
        var rows = ReceiptLayoutKeys.TotalRowKeys
            .Select(key => seen.TryGetValue(key, out var model)
                ? model
                : defaults.Rows.First(candidate => candidate.Key == key))
            .ToList();

        return new ReceiptTotalsLayout(rows);
    }

    // ── fonts ────────────────────────────────────────────────────────────────

    private static ReceiptFontSizes MergeFonts(ReceiptFontSizesPatch? patch, ReceiptFontSizes defaults, List<string> errors)
    {
        patch ??= new ReceiptFontSizesPatch(null, null, null, null, null);
        return new ReceiptFontSizes(
            Header: ValidateFont(patch.Header, defaults.Header, "اندازه فونت نام کافه", errors),
            Info: ValidateFont(patch.Info, defaults.Info, "اندازه فونت اطلاعات فیش مشتری", errors),
            Item: ValidateFont(patch.Item, defaults.Item, "اندازه فونت آیتم‌های فیش مشتری", errors),
            Subtotal: ValidateFont(patch.Subtotal, defaults.Subtotal, "اندازه فونت جمع اقلام", errors),
            Total: ValidateFont(patch.Total, defaults.Total, "اندازه فونت مبلغ نهایی", errors));
    }

    private static BaristaFontSizes MergeBaristaFonts(BaristaFontSizesPatch? patch, BaristaFontSizes defaults, List<string> errors)
    {
        patch ??= new BaristaFontSizesPatch(null, null, null, null);
        return new BaristaFontSizes(
            Title: ValidateFont(patch.Title, defaults.Title, "اندازه فونت عنوان فیش آشپزخانه", errors),
            Code: ValidateFont(patch.Code, defaults.Code, "اندازه فونت کد سفارش فیش آشپزخانه", errors),
            Info: ValidateFont(patch.Info, defaults.Info, "اندازه فونت اطلاعات فیش آشپزخانه", errors),
            Item: ValidateFont(patch.Item, defaults.Item, "اندازه فونت آیتم‌های فیش آشپزخانه", errors));
    }

    private static int ValidateFont(int? value, int fallback, string name, List<string> errors)
    {
        if (value is null)
        {
            return fallback;
        }

        if (value < ReceiptLayoutKeys.MinFontSize || value > ReceiptLayoutKeys.MaxFontSize)
        {
            errors.Add($"{name} باید عددی بین {ReceiptLayoutKeys.MinFontSize} تا {ReceiptLayoutKeys.MaxFontSize} باشد.");
            return fallback;
        }

        return value.Value;
    }

    // ── shared helpers ───────────────────────────────────────────────────────

    private static string ValidateLabel(string? posted, string fallback, string name, List<string> errors)
    {
        if (posted is null)
        {
            return fallback;
        }

        var trimmed = posted.Trim();
        if (trimmed.Length > ReceiptLayoutKeys.MaxLabelLength)
        {
            errors.Add($"{name} حداکثر می‌تواند {ReceiptLayoutKeys.MaxLabelLength} نویسه باشد.");
            return fallback;
        }

        return trimmed;
    }

    private static List<string> FilterKnownKeys(List<string>? posted, IReadOnlyList<string> allowed, string listName, List<string> errors)
    {
        if (posted is null)
        {
            return [.. allowed];
        }

        var result = new List<string>();
        foreach (var postedKey in posted)
        {
            var key = (postedKey ?? string.Empty).Trim();
            if (!allowed.Contains(key))
            {
                errors.Add($"«{key}» در {listName} شناسهٔ مجازی ندارد؛ شناسه‌های مجاز: {string.Join("، ", allowed)}.");
                continue;
            }

            if (!result.Contains(key))
            {
                result.Add(key);
            }
        }

        return result;
    }
}
