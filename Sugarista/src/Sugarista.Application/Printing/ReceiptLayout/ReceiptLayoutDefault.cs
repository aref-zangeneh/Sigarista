namespace Sugarista.Application.Printing.ReceiptLayout;

/// <summary>Built-in layout defaults, identical to the classic printed receipt.</summary>
public static class ReceiptLayoutDefault
{
    public static ResolvedReceiptLayout All() => new(Customer(), Barista());

    public static CustomerReceiptLayout Customer() => new(
        Header: new CustomerHeaderLayout(ShowAddress: true, ShowPhone: true),
        HeaderRows:
        [
            ReceiptLayoutKeys.HeaderRowCode,
            ReceiptLayoutKeys.HeaderRowCreatedAt,
            ReceiptLayoutKeys.HeaderRowChannel,
            ReceiptLayoutKeys.HeaderRowService,
            ReceiptLayoutKeys.HeaderRowTable,
            ReceiptLayoutKeys.HeaderRowPickupTime,
            ReceiptLayoutKeys.HeaderRowCustomerName,
            ReceiptLayoutKeys.HeaderRowCustomerPhone,
            ReceiptLayoutKeys.HeaderRowNote,
        ],
        Items: CustomerItems(),
        Totals: new ReceiptTotalsLayout(
        [
            new ReceiptTotalRowLayout(ReceiptLayoutKeys.TotalRowSubtotal, "جمع اقلام", true),
            new ReceiptTotalRowLayout(ReceiptLayoutKeys.TotalRowDeliveryFee, "هزینه ارسال", true),
            new ReceiptTotalRowLayout(ReceiptLayoutKeys.TotalRowTax, "مالیات", true),
            new ReceiptTotalRowLayout(ReceiptLayoutKeys.TotalRowTotal, "مبلغ نهایی", true),
        ]),
        PaymentRows:
        [
            ReceiptLayoutKeys.PaymentRowMethod,
            ReceiptLayoutKeys.PaymentRowReference,
            ReceiptLayoutKeys.PaymentRowPaid,
        ],
        Footer: new CustomerFooterLayout(Text: string.Empty, ShowThanks: true, ShowCafeName: true),
        Fonts: new ReceiptFontSizes(Header: 40, Info: 22, Item: 30, Subtotal: 24, Total: 36));

    public static ReceiptItemsLayout CustomerItems() => new(
        Columns:
        [
            new ReceiptColumnLayout(ReceiptLayoutKeys.ColumnName, string.Empty, true, null),
            new ReceiptColumnLayout(ReceiptLayoutKeys.ColumnQty, "تعداد", true, null),
            new ReceiptColumnLayout(ReceiptLayoutKeys.ColumnUnitPrice, "واحد", false, null),
            new ReceiptColumnLayout(ReceiptLayoutKeys.ColumnTotal, "جمع", true, null),
        ],
        SubLines:
        [
            ReceiptLayoutKeys.SubLineSize,
            ReceiptLayoutKeys.SubLineOptions,
            ReceiptLayoutKeys.SubLineModifiers,
            ReceiptLayoutKeys.SubLineCustomDrink,
            ReceiptLayoutKeys.SubLinePayer,
        ],
        ShowHeaderRow: false,
        ShowOptionPrices: false,
        ShowUnitPriceNote: true,
        FontSize: 30);

    public static BaristaReceiptLayout Barista() => new(
        Title: "فیش باریستا",
        InfoRows:
        [
            ReceiptLayoutKeys.InfoRowPaidAt,
            ReceiptLayoutKeys.InfoRowChannel,
            ReceiptLayoutKeys.InfoRowService,
            ReceiptLayoutKeys.InfoRowTable,
            ReceiptLayoutKeys.InfoRowCustomerName,
        ],
        Items: new ReceiptItemsLayout(
            Columns:
            [
                new ReceiptColumnLayout(ReceiptLayoutKeys.ColumnName, string.Empty, true, null),
                new ReceiptColumnLayout(ReceiptLayoutKeys.ColumnQty, string.Empty, true, null),
            ],
            SubLines:
            [
                ReceiptLayoutKeys.SubLineSize,
                ReceiptLayoutKeys.SubLineModifiers,
                ReceiptLayoutKeys.SubLineOptions,
                ReceiptLayoutKeys.SubLineCustomDrink,
                ReceiptLayoutKeys.SubLineBaristaNote,
            ],
            ShowHeaderRow: false,
            ShowOptionPrices: false,
            ShowUnitPriceNote: false,
            FontSize: 38),
        ShowOrderNote: true,
        Footer: new BaristaFooterLayout(Text: string.Empty, ShowSign: true),
        Fonts: new BaristaFontSizes(Title: 26, Code: 56, Info: 24, Item: 38));
}
