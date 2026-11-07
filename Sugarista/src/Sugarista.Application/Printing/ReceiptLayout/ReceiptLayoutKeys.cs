namespace Sugarista.Application.Printing.ReceiptLayout;

/// <summary>
/// Allowed keys and built-in defaults of the receipt layout document.
/// These values reproduce the classic receipt exactly, so a café that never
/// touches the admin panel keeps the same printed output.
/// </summary>
public static class ReceiptLayoutKeys
{
    public const int Version = 1;

    // Customer receipt — rows before the items table.
    public const string HeaderRowCode = "code";
    public const string HeaderRowCreatedAt = "createdAt";
    public const string HeaderRowChannel = "channel";
    public const string HeaderRowService = "service";
    public const string HeaderRowTable = "table";
    public const string HeaderRowPickupTime = "pickupTime";
    public const string HeaderRowCustomerName = "customerName";
    public const string HeaderRowCustomerPhone = "customerPhone";
    public const string HeaderRowNote = "note";

    // Customer receipt — rows after the totals.
    public const string PaymentRowMethod = "paymentMethod";
    public const string PaymentRowReference = "reference";
    public const string PaymentRowPaid = "amountPaid";

    // Item table columns. qty is drawn inline ("۲ × اسپرسو"); unitPrice/total
    // form real table columns.
    public const string ColumnName = "name";
    public const string ColumnQty = "qty";
    public const string ColumnUnitPrice = "unitPrice";
    public const string ColumnTotal = "total";

    // Detail lines under an item name.
    public const string SubLineSize = "size";
    public const string SubLineOptions = "options";
    public const string SubLineModifiers = "modifiers";
    public const string SubLineCustomDrink = "customDrink";
    public const string SubLinePayer = "payer";
    public const string SubLineBaristaNote = "baristaNote";

    // Totals rows.
    public const string TotalRowSubtotal = "subtotal";
    public const string TotalRowDeliveryFee = "deliveryFee";
    public const string TotalRowTax = "tax";
    public const string TotalRowTotal = "total";

    // Barista ticket info rows.
    public const string InfoRowCreatedAt = "createdAt";
    public const string InfoRowPaidAt = "paidAt";
    public const string InfoRowChannel = "channel";
    public const string InfoRowService = "service";
    public const string InfoRowTable = "table";
    public const string InfoRowCustomerName = "customerName";
    public const string InfoRowCustomerPhone = "customerPhone";
    public const string InfoRowPaymentMethod = "paymentMethod";

    public static readonly IReadOnlyList<string> CustomerHeaderRows =
    [
        HeaderRowCode, HeaderRowCreatedAt, HeaderRowChannel, HeaderRowService,
        HeaderRowTable, HeaderRowPickupTime, HeaderRowCustomerName,
        HeaderRowCustomerPhone, HeaderRowNote,
    ];

    public static readonly IReadOnlyList<string> CustomerPaymentRows =
        [PaymentRowMethod, PaymentRowReference, PaymentRowPaid];

    public static readonly IReadOnlyList<string> ItemColumnKeys =
        [ColumnName, ColumnQty, ColumnUnitPrice, ColumnTotal];

    /// <summary>Columns that form real table bands (name/qty are handled specially).</summary>
    public static readonly IReadOnlyList<string> TableBandColumnKeys =
        [ColumnUnitPrice, ColumnTotal];

    public static readonly IReadOnlyList<string> CustomerSubLines =
        [SubLineSize, SubLineOptions, SubLineModifiers, SubLineCustomDrink, SubLinePayer];

    public static readonly IReadOnlyList<string> BaristaSubLines =
        [SubLineSize, SubLineModifiers, SubLineOptions, SubLineCustomDrink, SubLineBaristaNote];

    public static readonly IReadOnlyList<string> TotalRowKeys =
        [TotalRowSubtotal, TotalRowDeliveryFee, TotalRowTax, TotalRowTotal];

    public static readonly IReadOnlyList<string> BaristaInfoRows =
    [
        InfoRowPaidAt, InfoRowCreatedAt, InfoRowChannel, InfoRowService,
        InfoRowTable, InfoRowCustomerName, InfoRowCustomerPhone, InfoRowPaymentMethod,
    ];

    public const int MinFontSize = 12;
    public const int MaxFontSize = 90;
    public const int MinWidthPct = 15;
    public const int MaxWidthPct = 90;
    public const int MaxLabelLength = 40;
}
