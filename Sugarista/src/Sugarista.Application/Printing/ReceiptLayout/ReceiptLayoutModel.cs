using Sugarista.Domain.Exceptions;

namespace Sugarista.Application.Printing.ReceiptLayout;

// ─────────────────────────────────────────────────────────────────────────────
// Resolved (fully concrete) layout document — the single shape stored in
// SiteSettings.ReceiptLayoutJson, returned by GET, accepted by PUT and
// embedded into every receipt print payload. The offline print agent renders
// exactly this shape; unknown/absent documents fall back to
// <see cref="ReceiptLayoutDefault.Customer"/> / <see cref="ReceiptLayoutDefault.Barista"/>.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Full receipt layout: one configuration per printed copy.</summary>
public sealed record ResolvedReceiptLayout(
    CustomerReceiptLayout Customer,
    BaristaReceiptLayout Barista);

/// <summary>Layout of the full customer receipt (the printed invoice).</summary>
public sealed record CustomerReceiptLayout(
    CustomerHeaderLayout Header,
    /// <summary>Ordered keys shown between the title divider and the items table.</summary>
    List<string> HeaderRows,
    ReceiptItemsLayout Items,
    ReceiptTotalsLayout Totals,
    /// <summary>Ordered payment rows drawn under the totals (moneyfree notes live in HeaderRows).</summary>
    List<string> PaymentRows,
    CustomerFooterLayout Footer,
    ReceiptFontSizes Fonts);

/// <summary>Cafe identity block of the customer receipt.</summary>
public sealed record CustomerHeaderLayout(
    bool ShowAddress,
    bool ShowPhone);

/// <summary>One column of the receipt items table.</summary>
/// <param name="Key">Column key: name, qty, unitPrice or total.</param>
/// <param name="Label">Persian column header (used when ShowHeaderRow is on).</param>
/// <param name="Visible">Whether the column is rendered.</param>
/// <param name="WidthPct">Optional percent of the printable width for money columns.</param>
public sealed record ReceiptColumnLayout(
    string Key,
    string Label,
    bool Visible,
    int? WidthPct);

/// <summary>Items table of both receipt copies.</summary>
public sealed record ReceiptItemsLayout(
    List<ReceiptColumnLayout> Columns,
    /// <summary>Ordered detail lines under the item name: size, options, modifiers, customDrink, payer, baristaNote.</summary>
    List<string> SubLines,
    bool ShowHeaderRow,
    /// <summary>When on, each selected option displays its own price delta (+۵۰٬۰۰۰).</summary>
    bool ShowOptionPrices,
    /// <summary>When on and the unit price column is hidden, qty>1 lines show a "واحد: …" note.</summary>
    bool ShowUnitPriceNote,
    int FontSize);

/// <summary>Order-total rows of the customer receipt.</summary>
public sealed record ReceiptTotalsLayout(List<ReceiptTotalRowLayout> Rows);

/// <summary>One total row; Key = subtotal, deliveryFee, tax or total.</summary>
public sealed record ReceiptTotalRowLayout(string Key, string Label, bool Visible);

/// <summary>Thank-you block at the bottom of the customer receipt.</summary>
public sealed record CustomerFooterLayout(
    /// <summary>Overrides SiteSettings.FooterText when non-empty.</summary>
    string Text,
    bool ShowThanks,
    bool ShowCafeName);

/// <summary>Font sizes (pt of the 384/576-dot bitmap) of the customer receipt.</summary>
public sealed record ReceiptFontSizes(
    int Header,
    int Info,
    int Item,
    int Subtotal,
    int Total);

/// <summary>Layout of the barista/kitchen ticket.</summary>
public sealed record BaristaReceiptLayout(
    /// <summary>Big headline above the order code.</summary>
    string Title,
    /// <summary>Ordered keys: paidAt, createdAt, channel, service, table, customerName, customerPhone, paymentMethod.</summary>
    List<string> InfoRows,
    ReceiptItemsLayout Items,
    bool ShowOrderNote,
    BaristaFooterLayout Footer,
    BaristaFontSizes Fonts);

/// <summary>Bottom block of the barista ticket.</summary>
public sealed record BaristaFooterLayout(
    string Text,
    bool ShowSign);

/// <summary>Font sizes (pt of the bitmap) of the barista ticket.</summary>
public sealed record BaristaFontSizes(
    int Title,
    int Code,
    int Info,
    int Item);
