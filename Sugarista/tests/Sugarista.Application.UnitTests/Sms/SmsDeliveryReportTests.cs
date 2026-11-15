using Sugarista.Application.Sms;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.UnitTests.Sms;

public sealed class SmsDeliveryStatusParserTests
{
    [Theory]
    [InlineData("delivered", null, SmsOutcomeStatus.Delivered)]
    [InlineData("Delivered", null, SmsOutcomeStatus.Delivered)]
    [InlineData("failed", null, SmsOutcomeStatus.Failed)]
    [InlineData("Rejected", null, SmsOutcomeStatus.Rejected)]
    [InlineData("sent", null, SmsOutcomeStatus.Sent)]
    [InlineData("whatever", 5, SmsOutcomeStatus.Delivered)]       // Kavenegar 5 = delivered
    [InlineData("whatever", 4, SmsOutcomeStatus.Sent)]            // Kavenegar 4 = sent
    [InlineData("whatever", 6, SmsOutcomeStatus.Failed)]          // Kavenegar 6 = failed
    [InlineData("whatever", 11, SmsOutcomeStatus.Rejected)]       // Kavenegar 11 = rejected
    [InlineData("", 99, SmsOutcomeStatus.Unknown)]
    public void Parse_TextWinsOverNumbers_AndNumericMapsPerProvider(
        string statusText,
        int? statusCode,
        SmsOutcomeStatus expected)
    {
        Assert.Equal(
            expected,
            SmsDeliveryStatusParser.Parse(SmsProviderType.Kavenegar, statusText, statusCode));
    }

    [Theory]
    [InlineData(1, SmsOutcomeStatus.Delivered)]
    [InlineData(2, SmsOutcomeStatus.Failed)]
    [InlineData(7, SmsOutcomeStatus.Unknown)]
    public void Parse_NonKavenegarProviders_UseOneIsDeliveredConvention(int statusCode, SmsOutcomeStatus expected)
    {
        Assert.Equal(
            expected,
            SmsDeliveryStatusParser.Parse(SmsProviderType.SmsIr, null, statusCode));
        Assert.Equal(
            expected,
            SmsDeliveryStatusParser.Parse(SmsProviderType.Ghasedak, null, statusCode));
        Assert.Equal(
            expected,
            SmsDeliveryStatusParser.Parse(SmsProviderType.IpPanel, null, statusCode));
    }

    [Fact]
    public void Parse_NothingUsable_ReturnsUnknown()
    {
        Assert.Equal(
            SmsOutcomeStatus.Unknown,
            SmsDeliveryStatusParser.Parse(SmsProviderType.Melipayamak, null, null));
    }
}

public sealed class SmsSmartContactFilterTests
{
    [Fact]
    public void ToJson_FromJson_RoundTrips()
    {
        var filter = new SmsSmartContactFilter(true, false, 30);

        var restored = SmsSmartContactFilter.FromJsonOrNull(filter.ToJson());

        Assert.Equal(filter, restored);
    }

    [Fact]
    public void FromJsonOrNull_InvalidJson_ReturnsNull()
    {
        Assert.Null(SmsSmartContactFilter.FromJsonOrNull("not-json"));
        Assert.Null(SmsSmartContactFilter.FromJsonOrNull(null));
        Assert.Null(SmsSmartContactFilter.FromJsonOrNull(""));
    }

    [Fact]
    public void IsEmpty_TrueWhenNoPredicateSet()
    {
        Assert.True(SmsSmartContactFilter.Default.IsEmpty);
        Assert.False(new SmsSmartContactFilter(true, null, null).IsEmpty);
        Assert.False(new SmsSmartContactFilter(null, null, 14).IsEmpty);
        Assert.True(new SmsSmartContactFilter(null, null, 0).IsEmpty); // 0 days = no filter
    }

    [Fact]
    public void Describe_ProducesPersianSummary()
    {
        Assert.Equal("همهٔ مخاطبین", SmsSmartContactFilter.Default.Describe());
        Assert.Contains("رضایت", new SmsSmartContactFilter(null, true, null).Describe());
        Assert.Contains("۳۰", new SmsSmartContactFilter(null, null, 30).Describe());
    }
}
