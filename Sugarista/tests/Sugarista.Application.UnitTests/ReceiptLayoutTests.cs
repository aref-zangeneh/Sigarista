using Sugarista.Application.Printing;
using Sugarista.Application.Printing.Commands.UpdateReceiptLayout;
using Sugarista.Application.Printing.Queries.GetReceiptLayout;
using Sugarista.Application.Printing.ReceiptLayout;
using Sugarista.Domain.Exceptions;
using System.Linq;

namespace Sugarista.Application.UnitTests;

/// <summary>Unit tests for the dynamic receipt layout document (TASK-014).</summary>
public class ReceiptLayoutTests
{
    // ── defaults & merge ─────────────────────────────────────────────────────

    [Fact]
    public void Resolve_NullPatch_ProducesClassicDefaults()
    {
        var resolved = ReceiptLayoutResolver.Resolve(null);

        Assert.Equal(
            ReceiptLayoutJsonParser.Serialize(ReceiptLayoutDefault.Customer()),
            ReceiptLayoutJsonParser.Serialize(resolved.Customer));
        Assert.Equal(
            ReceiptLayoutJsonParser.Serialize(ReceiptLayoutDefault.Barista()),
            ReceiptLayoutJsonParser.Serialize(resolved.Barista));
    }

    [Fact]
    public void Resolve_PartialCustomerPatch_FillsMissingSectionsFromDefaults()
    {
        var resolved = ReceiptLayoutResolver.Resolve(
            new ReceiptLayoutPatch(
                Customer: new CustomerReceiptPatch(
                    Header: new CustomerHeaderPatch(ShowAddress: false, ShowPhone: null),
                    HeaderRows: null,
                    Items: new ReceiptItemsPatch(
                        Columns: null,
                        SubLines: null,
                        ShowHeaderRow: true,
                        ShowOptionPrices: true,
                        ShowUnitPriceNote: null,
                        FontSize: null),
                    Totals: null,
                    PaymentRows: null,
                    Footer: new CustomerFooterPatch(Text: "با تشکر", ShowThanks: null, ShowCafeName: null, ThanksText: "نوش جان"),
                    Fonts: null),
                Barista: null));

        Assert.False(resolved.Customer.Header.ShowAddress);
        Assert.True(resolved.Customer.Header.ShowPhone);
        Assert.Equal(ReceiptLayoutDefault.Customer().HeaderRows, resolved.Customer.HeaderRows);
        Assert.True(resolved.Customer.Items.ShowHeaderRow);
        Assert.True(resolved.Customer.Items.ShowOptionPrices);
        Assert.True(resolved.Customer.Items.ShowUnitPriceNote); // null patch keeps the default
        Assert.Equal("با تشکر", resolved.Customer.Footer.Text);
        Assert.Equal("نوش جان", resolved.Customer.Footer.ThanksText);
        Assert.Equal(
            ReceiptLayoutJsonParser.Serialize(ReceiptLayoutDefault.Barista()),
            ReceiptLayoutJsonParser.Serialize(resolved.Barista));
    }

    [Fact]
    public void Resolve_RoundsTripThroughJsonKeepsConfiguration()
    {
        var resolved = ReceiptLayoutResolver.Resolve(
            new ReceiptLayoutPatch(
                Customer: new CustomerReceiptPatch(
                    Header: null,
                    HeaderRows: ["code", "pickupTime", "customerName", "note"],
                    Items: null,
                    Totals: null,
                    PaymentRows: [],
                    Footer: null,
                    Fonts: new ReceiptFontSizesPatch(Header: 44, Info: 20, Item: 28, Subtotal: 24, Total: 40)),
                Barista: new BaristaReceiptPatch(
                    Title: "سفارش آشپزخانه",
                    InfoRows: ["paidAt", "channel"],
                    Items: null,
                    ShowOrderNote: false,
                    Footer: null,
                    Fonts: new BaristaFontSizesPatch(Title: 30, Code: 48, Info: 24, Item: 40))));

        var storedJson = ReceiptLayoutJsonParser.Serialize(resolved);
        var parsed = ReceiptLayoutJsonParser.Parse(storedJson);

        Assert.Equal(storedJson, ReceiptLayoutJsonParser.Serialize(parsed));
        Assert.Equal("سفارش آشپزخانه", parsed.Barista.Title);
        Assert.False(parsed.Barista.ShowOrderNote);
        Assert.Empty(parsed.Customer.PaymentRows);
        Assert.Equal(["code", "pickupTime", "customerName", "note"], parsed.Customer.HeaderRows);
    }

    [Fact]
    public void Parse_InvalidOrEmptyJson_FallsBackToDefaults()
    {
        Assert.Equal(
            ReceiptLayoutJsonParser.Serialize(ReceiptLayoutDefault.All()),
            ReceiptLayoutJsonParser.Serialize(ReceiptLayoutJsonParser.Parse(null)));
        Assert.Equal(
            ReceiptLayoutJsonParser.Serialize(ReceiptLayoutDefault.All()),
            ReceiptLayoutJsonParser.Serialize(ReceiptLayoutJsonParser.Parse("")));
        Assert.Equal(
            ReceiptLayoutJsonParser.Serialize(ReceiptLayoutDefault.All()),
            ReceiptLayoutJsonParser.Serialize(ReceiptLayoutJsonParser.Parse("not json at all")));
        Assert.Equal(
            ReceiptLayoutJsonParser.Serialize(ReceiptLayoutDefault.All()),
            ReceiptLayoutJsonParser.Serialize(ReceiptLayoutJsonParser.Parse("{ truncated")));
    }

    [Fact]
    public void Parse_LegacyStoredJsonWithoutNewFields_UpgradesCleanlyWithDefaults()
    {
        var legacyJson = """
        {"customer":{"header":{"showAddress":false,"showPhone":true},"headerRows":["code","customerName"],"items":{"columns":[{"key":"name","label":"","visible":true},{"key":"total","label":"جمع","visible":true}],"subLines":["options"],"showHeaderRow":true,"showOptionPrices":true,"showUnitPriceNote":true,"fontSize":28},"totals":{"rows":[{"key":"subtotal","label":"جمع اقلام","visible":true},{"key":"total","label":"قابل پرداخت","visible":true}]},"paymentRows":["paymentMethod"],"footer":{"text":"پانویس قدیم","showThanks":true,"showCafeName":true},"fonts":{"header":34,"info":20,"item":28,"subtotal":22,"total":32}},"barista":{"title":"آشپزخانه","infoRows":["paidAt"],"items":{"columns":[{"key":"name","label":"","visible":true}],"subLines":["options"],"showHeaderRow":false,"showOptionPrices":false,"showUnitPriceNote":false,"fontSize":40},"showOrderNote":true,"footer":{"text":"","showSign":true},"fonts":{"title":30,"code":48,"info":24,"item":40}}}
        """;

        var parsed = ReceiptLayoutJsonParser.Parse(legacyJson);

        Assert.False(parsed.Customer.Header.ShowAddress);
        Assert.True(parsed.Customer.Header.ShowPhone);
        Assert.True(parsed.Customer.Header.ShowOrnament);
        Assert.Equal("صورت‌حساب سفارش", parsed.Customer.Header.Subtitle);
        Assert.Equal("پانویس قدیم", parsed.Customer.Footer.Text);
        Assert.Equal(22, parsed.Customer.Fonts.FooterCafe);
        Assert.Equal(24, parsed.Customer.Fonts.TopPadding);
        Assert.Equal(96, parsed.Customer.Fonts.BottomPadding);
        Assert.Equal("dashed", parsed.Customer.Style?.DividerStyle);
        Assert.Equal("☕", parsed.Customer.Style?.OrnamentIcon);
        Assert.Equal("rounded", parsed.Customer.Style?.CodeBoxStyle);
        Assert.Contains(parsed.Customer.Totals.Rows, r => r.Key == ReceiptLayoutKeys.TotalRowDiscount);
    }

    [Fact]
    public void Resolve_ModernBrandWifiQrPromoAndStyleFields_RoundTripCleanly()
    {
        var resolved = ReceiptLayoutResolver.Resolve(
            new ReceiptLayoutPatch(
                Customer: new CustomerReceiptPatch(
                    Header: new CustomerHeaderPatch(
                        ShowAddress: true,
                        ShowPhone: true,
                        CafeName: "کافه قنادی شوگاریستا",
                        Subtitle: "Specialty Coffee & Pastry",
                        CustomAddress: "تهران، جردن، پلاک ۵",
                        CustomPhone: "۰۲۱-۸۸۷۷۶۶۵۵",
                        TaxId: "۴۱۱۱۲۲۲۲۳۳۳۳",
                        ShowOrnament: true),
                    HeaderRows: null,
                    Items: null,
                    Totals: new ReceiptTotalsPatch(null, ShowItemCount: true, ShowSavingsBanner: true),
                    PaymentRows: null,
                    Footer: new CustomerFooterPatch(
                        Text: "هر روز ۸ تا ۲۳",
                        ShowThanks: true,
                        ShowCafeName: true,
                        ThanksText: "از حضور گرم شما سپاسگزاریم ✨",
                        CafeNameText: "کافه قنادی شوگاریستا — شعبه جردن",
                        ShowWifi: true,
                        WifiSsid: "Sugarista_Guest",
                        WifiPassword: "coffee2026",
                        InstagramHandle: "@sugarista.cafe",
                        WebsiteUrl: "sugarista.ir",
                        PromoBanner: "با ارائه این فیش ۱۰٪ تخفیف در سفارش بعدی بگیرید!",
                        ShowQrCode: true,
                        QrCodeUrl: "https://sugarista.ir",
                        QrCodeLabel: "اسکن برای منوی دیجیتال"),
                    Fonts: new ReceiptFontSizesPatch(42, 22, 30, 24, 36, FooterCafe: 24, TopPadding: 32, BottomPadding: 110),
                    Style: new CustomerStylePatch("dotted", "✨", "solid")),
                Barista: null));

        var json = ReceiptLayoutJsonParser.Serialize(resolved);
        var parsed = ReceiptLayoutJsonParser.Parse(json);

        Assert.Equal("کافه قنادی شوگاریستا", parsed.Customer.Header.CafeName);
        Assert.Equal("Specialty Coffee & Pastry", parsed.Customer.Header.Subtitle);
        Assert.Equal("۴۱۱۱۲۲۲۲۳۳۳۳", parsed.Customer.Header.TaxId);
        Assert.True(parsed.Customer.Totals.ShowItemCount);
        Assert.True(parsed.Customer.Footer.ShowWifi);
        Assert.Equal("Sugarista_Guest", parsed.Customer.Footer.WifiSsid);
        Assert.Equal("@sugarista.cafe", parsed.Customer.Footer.InstagramHandle);
        Assert.True(parsed.Customer.Footer.ShowQrCode);
        Assert.Equal("https://sugarista.ir", parsed.Customer.Footer.QrCodeUrl);
        Assert.Equal("کافه قنادی شوگاریستا — شعبه جردن", parsed.Customer.Footer.CafeNameText);
        Assert.Equal(32, parsed.Customer.Fonts.TopPadding);
        Assert.Equal(110, parsed.Customer.Fonts.BottomPadding);
        Assert.Equal("dotted", parsed.Customer.Style?.DividerStyle);
        Assert.Equal("solid", parsed.Customer.Style?.CodeBoxStyle);
    }

    // ── validation ───────────────────────────────────────────────────────────

    [Fact]
    public void Resolve_UnknownHeaderRowKey_ThrowsWithPersianMessage()
    {
        var exception = Assert.Throws<DomainException>(() => ReceiptLayoutResolver.Resolve(
            new ReceiptLayoutPatch(
                Customer: new CustomerReceiptPatch(null, ["code", "flyingPig"], null, null, null, null, null), null)));

        Assert.Contains("سطرهای اطلاعات فیش مشتری", exception.Message);
        Assert.Contains("flyingPig", exception.Message);
    }

    [Fact]
    public void Resolve_HiddenTotalOnCustomerReceipt_IsRejected()
    {
        var exception = Assert.Throws<DomainException>(() => ReceiptLayoutResolver.Resolve(
            new ReceiptLayoutPatch(
                Customer: new CustomerReceiptPatch(
                    null, null,
                    new ReceiptItemsPatch(
                        [
                            new ReceiptColumnPatch("name", null, true, null),
                            new ReceiptColumnPatch("qty", null, true, null),
                            new ReceiptColumnPatch("total", null, false, null),
                        ], null, null, null, null, null),
                    null, null, null, null),
                null)));

        Assert.Contains("ستون «جمع»", exception.Message);
    }

    [Fact]
    public void Resolve_HiddenTotalRow_IsRejected()
    {
        var exception = Assert.Throws<DomainException>(() => ReceiptLayoutResolver.Resolve(
            new ReceiptLayoutPatch(
                Customer: new CustomerReceiptPatch(
                    null, null, null,
                    new ReceiptTotalsPatch(
                    [
                        new ReceiptTotalRowPatch("subtotal", null, true),
                        new ReceiptTotalRowPatch("total", null, false),
                    ]),
                    null, null, null),
                null)));

        Assert.Contains("ردیف «مبلغ نهایی»", exception.Message);
    }

    [Fact]
    public void Resolve_MissingTotalsSection_KeepsDefaultTotalRow()
    {
        var resolved = ReceiptLayoutResolver.Resolve(new ReceiptLayoutPatch(null, null));

        Assert.Equal(
            new ReceiptTotalRowLayout("total", "مبلغ قابل پرداخت", true),
            resolved.Customer.Totals.Rows.First(row => row.Key == "total"));
    }

    [Fact]
    public void Resolve_MoneyColumnOnBaristaTicket_IsRejected()
    {
        var exception = Assert.Throws<DomainException>(() => ReceiptLayoutResolver.Resolve(
            new ReceiptLayoutPatch(
                null,
                new BaristaReceiptPatch(
                    null, null,
                    new ReceiptItemsPatch(
                        [
                            new ReceiptColumnPatch("name", null, true, null),
                            new ReceiptColumnPatch("total", null, true, null),
                        ],
                        null, null, null, null, null),
                    null, null, null))));

        Assert.Contains("فیش آشپزخانه ستون مبلغی ندارد", exception.Message);
    }

    [Fact]
    public void Resolve_DuplicateColumnsAndRows_AreRejected()
    {
        var exception = Assert.Throws<DomainException>(() => ReceiptLayoutResolver.Resolve(
            new ReceiptLayoutPatch(
                Customer: new CustomerReceiptPatch(
                    null, null,
                    new ReceiptItemsPatch(
                        [
                            new ReceiptColumnPatch("name", null, true, null),
                            new ReceiptColumnPatch("name", null, true, null),
                        ], null, null, null, null, null),
                    new ReceiptTotalsPatch(
                    [
                        new ReceiptTotalRowPatch("subtotal", null, true),
                        new ReceiptTotalRowPatch("subtotal", null, true),
                    ]),
                    null, null, null),
                null)));

        Assert.Contains("تکراری", exception.Message);
    }

    [Fact]
    public void Resolve_FontSizeOutOfRange_Throws()
    {
        var exception = Assert.Throws<DomainException>(() => ReceiptLayoutResolver.Resolve(
            new ReceiptLayoutPatch(
                Customer: new CustomerReceiptPatch(
                    null, null,
                    new ReceiptItemsPatch(null, null, null, null, null, 120),
                    null, null, null, null),
                null)));

        Assert.Contains("اندازه فونت آیتم‌های فیش مشتری", exception.Message);
    }

    [Fact]
    public void Resolve_WidthPctOutOfRange_ThrowsWithRangeMessage()
    {
        var exception = Assert.Throws<DomainException>(() => ReceiptLayoutResolver.Resolve(
            new ReceiptLayoutPatch(
                Customer: new CustomerReceiptPatch(
                    null, null,
                    new ReceiptItemsPatch(
                    [
                        new ReceiptColumnPatch("name", null, true, null),
                        new ReceiptColumnPatch("qty", null, true, null),
                        new ReceiptColumnPatch("total", null, true, 5),
                    ], null, null, null, null, null),
                    null, null, null, null),
                null)));

        Assert.Contains("عرض ستون «total»", exception.Message);
    }

    [Fact]
    public void Resolve_BaristaTitleTooLong_Throws()
    {
        var exception = Assert.Throws<DomainException>(() => ReceiptLayoutResolver.Resolve(
            new ReceiptLayoutPatch(
                null,
                new BaristaReceiptPatch(new string('ع', 61), null, null, null, null, null))));

        Assert.Contains("عنوان فیش آشپزخانه", exception.Message);
    }

    // ── TASK-013/TASK-012 contract guards ────────────────────────────────────

    [Fact]
    public void DefaultLayout_IncludesOptionsAndPickupTime()
    {
        var resolved = ReceiptLayoutDefault.All();

        // TASK-013: option groups must have a dedicated detail line on both copies.
        Assert.Contains("options", resolved.Customer.Items.SubLines);
        Assert.Contains("options", resolved.Barista.Items.SubLines);

        // Custom drink builder summary participates in both copies as well.
        Assert.Contains("customDrink", resolved.Customer.Items.SubLines);
        Assert.Contains("customDrink", resolved.Barista.Items.SubLines);

        // TASK-012 (pickup scheduling): a scheduled pickup time column exists.
        Assert.Contains("pickupTime", resolved.Customer.HeaderRows);
    }
}
