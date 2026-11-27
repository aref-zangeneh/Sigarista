using Sugarista.Application.Affiliates.Portal;
using Sugarista.Domain.Entities;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Application.UnitTests.Affiliates;

public class AffiliatePortalRulesTests
{
    [Theory]
    [InlineData("IR123456789012345678901234", "IR123456789012345678901234")]
    [InlineData("ir 1234-5678-9012-3456-7890-1234", "IR123456789012345678901234")]
    [InlineData("۱۲۳۴۵۶۷۸۹۰۱۲۳۴۵۶۷۸۹۰۱۲۳۴", "IR123456789012345678901234")]
    public void NormalizeAndValidateSheba_AcceptsValidFormats(string input, string expected)
    {
        Assert.Equal(expected, AffiliatePortalRules.NormalizeAndValidateSheba(input));
    }

    [Theory]
    [InlineData("")]
    [InlineData("IR12345")]
    [InlineData("IR1234567890123456789012345")]
    public void NormalizeAndValidateSheba_RejectsInvalidFormats(string input)
    {
        Assert.Throws<DomainException>(() => AffiliatePortalRules.NormalizeAndValidateSheba(input));
    }

    [Fact]
    public void ComputeCreditConversion_AddsBonusPercentageCorrectly()
    {
        var (bonus, total) = AffiliatePortalRules.ComputeCreditConversion(100_000m, 20m);
        Assert.Equal(20_000m, bonus);
        Assert.Equal(120_000m, total);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(0, 5, 0)]
    [InlineData(200, 15, 7.5)]
    [InlineData(10, 10, 100)]
    public void ComputeConversionRate_CalculatesPercentageSafely(long clicks, int orders, decimal expected)
    {
        Assert.Equal(expected, AffiliatePortalRules.ComputeConversionRate(clicks, orders));
    }

    [Fact]
    public void NormalizeSubId_CleansValidAndRejectsEmpty()
    {
        Assert.Equal("story-1", AffiliatePortalRules.NormalizeSubId(" Story 1 "));
        Assert.Equal("telegram", AffiliatePortalRules.NormalizeSubId("TELEGRAM"));
        Assert.Throws<DomainException>(() => AffiliatePortalRules.NormalizeSubId("   "));
    }

    [Fact]
    public void AffiliateMediaAsset_EnforcesFileOrCaptionByType()
    {
        var img = new AffiliateMediaAsset("پوستر لاته", AffiliateMediaAssetType.Image, "/uploads/latte.jpg");
        Assert.Equal("پوستر لاته", img.Title);

        var copy = new AffiliateMediaAsset("متن استوری", AffiliateMediaAssetType.CopyText, captionText: "بهترین قهوه شهر!");
        Assert.Equal("بهترین قهوه شهر!", copy.CaptionText);

        Assert.Throws<DomainException>(() => new AffiliateMediaAsset("عکس بدون فایل", AffiliateMediaAssetType.Image));
        Assert.Throws<DomainException>(() => new AffiliateMediaAsset("متن خالی", AffiliateMediaAssetType.CopyText));
    }
}
