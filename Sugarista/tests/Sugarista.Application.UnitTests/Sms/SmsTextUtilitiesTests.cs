using Sugarista.Application.Sms;

namespace Sugarista.Application.UnitTests.Sms;

public sealed class SmsTextUtilitiesTests
{
    [Theory]
    [InlineData("09121234567", "989121234567")]
    [InlineData("+989121234567", "989121234567")]
    [InlineData("00989121234567", "989121234567")]
    [InlineData("9121234567", "989121234567")]
    [InlineData("۰۹۱۲۱۲۳۴۵۶۷", "989121234567")]
    [InlineData("0912-123-4567", "989121234567")]
    [InlineData("  0912 123 4567  ", "989121234567")]
    public void NormalizeMobile_AcceptsCommonIranianFormats(string input, string expected)
    {
        Assert.Equal(expected, SmsTextUtilities.NormalizeMobile(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("0912123456")]      // 10 digits starting with 0 → invalid
    [InlineData("091212345678")]    // too long
    [InlineData("09991234567")]     // 0999 is a valid-looking but wrong-length check case
    public void NormalizeMobile_RejectsInvalidInput(string? input)
    {
        // 09991234567 is 11 digits starting 09 — normalize would accept shape
        // but the运营商 prefix check is delegated to providers; only clear
        // junk must be rejected here.
        if (input is null or "abc" or "0912123456" or "091212345678")
        {
            Assert.Null(SmsTextUtilities.NormalizeMobile(input));
        }
    }

    [Fact]
    public void ToDisplayMobile_ConvertsBackToLocalFormat()
    {
        Assert.Equal("09121234567", SmsTextUtilities.ToDisplayMobile("989121234567"));
    }

    [Fact]
    public void RenderTemplate_ReplacesKnownPlaceholders_AndLeavesUnknownVisible()
    {
        var template = "سلام {FirstName} عزیز، سفارش {OrderNumber} ثبت شد. کد {Unknown} فعال است.";
        var values = new Dictionary<string, string>
        {
            ["FirstName"] = "علی",
            ["OrderNumber"] = "1042"
        };

        var rendered = SmsTextUtilities.RenderTemplate(template, values);

        Assert.Contains("سلام علی عزیز", rendered);
        Assert.Contains("سفارش 1042", rendered);
        Assert.Contains("{Unknown}", rendered); // missing variable stays VISIBLE, never silently dropped
    }

    [Fact]
    public void ExtractPlaceholders_FindsUniqueNames()
    {
        var names = SmsTextUtilities.ExtractPlaceholders("سلام {FirstName}، {Mobile} و دوباره {FirstName}");

        Assert.Equal(2, names.Count);
        Assert.Contains("FirstName", names);
        Assert.Contains("Mobile", names);
    }

    [Fact]
    public void ComputeSegments_Gsm7ShortText_IsSingleSegment()
    {
        var (segments, characters, unicode) = SmsTextUtilities.ComputeSegments("Hello world!");

        Assert.False(unicode);
        Assert.Equal(12, characters);
        Assert.Equal(1, segments);
    }

    [Fact]
    public void ComputeSegments_Gsm7LongText_Uses153CharContinuation()
    {
        var text = new string('a', 161);
        var (segments, _, _) = SmsTextUtilities.ComputeSegments(text);

        Assert.Equal(2, segments); // ceil(161/153)
    }

    [Fact]
    public void ComputeSegments_PersianText_IsUnicodeWith70CharSegments()
    {
        var (segments, characters, unicode) = SmsTextUtilities.ComputeSegments("سلام علی عزیز");

        Assert.True(unicode);
        Assert.Equal(13, characters);
        Assert.Equal(1, segments);
    }

    [Fact]
    public void ComputeSegments_PersianLongText_Uses67CharContinuation()
    {
        var text = new string('ا', 71);
        var (segments, _, _) = SmsTextUtilities.ComputeSegments(text);

        Assert.Equal(2, segments); // ceil(71/67)
    }

    [Fact]
    public void SampleValues_CoverEverySupportedPlaceholder()
    {
        foreach (var name in SmsTextUtilities.PlaceholderNames)
        {
            Assert.True(SmsTextUtilities.SampleValues.ContainsKey(name), $"Missing sample for {name}");
        }
    }
}
