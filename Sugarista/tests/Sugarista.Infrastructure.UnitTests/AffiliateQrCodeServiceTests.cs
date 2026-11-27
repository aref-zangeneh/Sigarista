using Sugarista.Infrastructure.Services;

namespace Sugarista.Infrastructure.UnitTests;

public class AffiliateQrCodeServiceTests
{
    private readonly AffiliateQrCodeService _service = new();

    [Fact]
    public void GenerateSvg_ProducesValidCrispSvgMarkup()
    {
        var svg = _service.GenerateSvg("https://sugarista.ir/r/ali?src=story1", 512);

        Assert.StartsWith("<svg ", svg);
        Assert.EndsWith("</svg>", svg);
        Assert.Contains("width=\"512\"", svg);
        Assert.Contains("<path fill=\"#0F172A\"", svg);
    }

    [Fact]
    public void GeneratePng_ProducesValidHighDpiPngBytes()
    {
        var bytes = _service.GeneratePng("https://sugarista.ir/r/ali?src=story1", 512);

        // PNG signature: 89 50 4E 47 0D 0A 1A 0A
        Assert.True(bytes.Length > 200);
        Assert.Equal(0x89, bytes[0]);
        Assert.Equal(0x50, bytes[1]);
        Assert.Equal(0x4E, bytes[2]);
        Assert.Equal(0x47, bytes[3]);
    }
}
