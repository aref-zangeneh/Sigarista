using Sugarista.Application.Common.Extensions;

namespace Sugarista.Application.UnitTests;

public sealed class ImageUrlNormalizerTests
{
    [Theory]
    [InlineData(null, "https://sugarista.ir", null)]
    [InlineData("", "https://sugarista.ir", null)]
    [InlineData("   ", "https://sugarista.ir", null)]
    public void NullOrWhitespace_StaysNull(string? raw, string origin, string? expected)
    {
        Assert.Equal(expected, ImageUrlNormalizer.Normalize(raw, origin));
    }

    [Fact]
    public void RootRelativePath_GetsTheSiteDomainPrepended()
    {
        var result = ImageUrlNormalizer.Normalize("/api/files/images/abc.webp", "https://sugarista.ir");

        Assert.Equal("https://sugarista.ir/api/files/images/abc.webp", result);
    }

    [Fact]
    public void BareRelativePath_GetsDomainAndSlash()
    {
        var result = ImageUrlNormalizer.Normalize("images/cat.webp", "https://sugarista.ir/");

        Assert.Equal("https://sugarista.ir/images/cat.webp", result);
    }

    [Theory]
    [InlineData("https://cdn.example.com/pic.webp")]
    [InlineData("http://cdn.example.com/pic.webp")]
    public void AbsoluteUrls_PassThroughUntouched(string raw)
    {
        Assert.Equal(raw, ImageUrlNormalizer.Normalize(raw, "https://sugarista.ir"));
    }

    [Fact]
    public void WithoutConfiguredOrigin_RelativeStaysRootRelative()
    {
        Assert.Equal("/api/files/images/abc.webp",
            ImageUrlNormalizer.Normalize("/api/files/images/abc.webp", null));
    }
}
