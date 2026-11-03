using Sugarista.Application.Carts.Services;

namespace Sugarista.Application.UnitTests;

public class AbandonedCartMessageTemplateTests
{
    private static readonly Dictionary<string, string> Values = new(StringComparer.OrdinalIgnoreCase)
    {
        ["name"] = "سارا",
        ["cafe"] = "شوگریستا",
        ["code"] = "CART-ABC123",
        ["percent"] = "10",
        ["validityHours"] = "48",
        ["itemCount"] = "2",
        ["itemsTotal"] = "250,000",
        ["items"] = "- لاته × 2"
    };

    [Fact]
    public void Render_ShouldReplaceAllPlaceholders()
    {
        var template = "سلام {name}، کد {code} برای {cafe} با {percent}٪ تخفیف تا {validityHours} ساعت.";

        var result = AbandonedCartMessageTemplate.Render(template, Values);

        Assert.Equal("سلام سارا، کد CART-ABC123 برای شوگریستا با 10٪ تخفیف تا 48 ساعت.", result);
    }

    [Fact]
    public void Render_ShouldBeCaseInsensitiveAndTrim()
    {
        var result = AbandonedCartMessageTemplate.Render("  {NAME} / {Code}  ", Values);

        Assert.Equal("سارا / CART-ABC123", result);
    }

    [Fact]
    public void Render_ShouldLeaveUnknownPlaceholdersUntouched()
    {
        var result = AbandonedCartMessageTemplate.Render("{name} {unknown}", Values);

        Assert.Equal("سارا {unknown}", result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Render_ShouldReturnEmptyForBlankTemplate(string? template)
    {
        Assert.Equal(string.Empty, AbandonedCartMessageTemplate.Render(template, Values));
    }

    [Fact]
    public void RenderHtml_ShouldEncodeMarkupAndConvertNewlines()
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["name"] = "<b>سارا</b>",
            ["items"] = "- لاته × 2\n- چیزکیک × 1"
        };

        var result = AbandonedCartMessageTemplate.RenderHtml(
            "سلام {name}\n{items}",
            values);

        Assert.Equal("سلام &lt;b&gt;سارا&lt;/b&gt;<br />- لاته × 2<br />- چیزکیک × 1", result);
    }

    [Fact]
    public void RenderHtml_ShouldReturnEmptyForBlankTemplate()
    {
        Assert.Equal(string.Empty, AbandonedCartMessageTemplate.RenderHtml("   ", Values));
    }
}
