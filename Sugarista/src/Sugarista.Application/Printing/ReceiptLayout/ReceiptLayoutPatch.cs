namespace Sugarista.Application.Printing.ReceiptLayout;

// ─────────────────────────────────────────────────────────────────────────────
// Partial (PATCH-style) layout documents posted by the admin panel. Every
// field may be missing; missing fields fall back to the built-in default, so
// the admin panel can send only the sections it edits.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Partial receipt layout.</summary>
public sealed record ReceiptLayoutPatch(
    CustomerReceiptPatch? Customer,
    BaristaReceiptPatch? Barista);

/// <summary>Partial customer-receipt layout.</summary>
public sealed record CustomerReceiptPatch(
    CustomerHeaderPatch? Header,
    List<string>? HeaderRows,
    ReceiptItemsPatch? Items,
    ReceiptTotalsPatch? Totals,
    List<string>? PaymentRows,
    CustomerFooterPatch? Footer,
    ReceiptFontSizesPatch? Fonts);

/// <summary>Partial cafe identity block.</summary>
public sealed record CustomerHeaderPatch(bool? ShowAddress, bool? ShowPhone);

/// <summary>Partial items table of both copies.</summary>
public sealed record ReceiptItemsPatch(
    List<ReceiptColumnPatch>? Columns,
    List<string>? SubLines,
    bool? ShowHeaderRow,
    bool? ShowOptionPrices,
    bool? ShowUnitPriceNote,
    int? FontSize);

/// <summary>Partial item column.</summary>
public sealed record ReceiptColumnPatch(
    string Key,
    string? Label,
    bool? Visible,
    int? WidthPct);

/// <summary>Partial totals block.</summary>
public sealed record ReceiptTotalsPatch(List<ReceiptTotalRowPatch>? Rows);

/// <summary>Partial total row; Key is required, the rest falls back.</summary>
public sealed record ReceiptTotalRowPatch(string Key, string? Label, bool? Visible);

/// <summary>Partial footer block.</summary>
public sealed record CustomerFooterPatch(string? Text, bool? ShowThanks, bool? ShowCafeName);

/// <summary>Partial font sizes of the customer receipt.</summary>
public sealed record ReceiptFontSizesPatch(int? Header, int? Info, int? Item, int? Subtotal, int? Total);

/// <summary>Partial barista ticket layout.</summary>
public sealed record BaristaReceiptPatch(
    string? Title,
    List<string>? InfoRows,
    ReceiptItemsPatch? Items,
    bool? ShowOrderNote,
    BaristaFooterPatch? Footer,
    BaristaFontSizesPatch? Fonts);

/// <summary>Partial barista footer.</summary>
public sealed record BaristaFooterPatch(string? Text, bool? ShowSign);

/// <summary>Partial barista font sizes.</summary>
public sealed record BaristaFontSizesPatch(int? Title, int? Code, int? Info, int? Item);
