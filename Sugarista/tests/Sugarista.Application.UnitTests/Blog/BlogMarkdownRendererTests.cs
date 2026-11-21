using Sugarista.Application.Blog;

namespace Sugarista.Application.UnitTests.Blog;

public class BlogMarkdownRendererTests
{
    [Fact]
    public void Render_ExtractsHeadingsIntoTocAndRendersAnchors()
    {
        const string markdown = """
            ## قهوه چیست؟
            یک نوشیدنی محبوب در جهان است.

            ### تفاوت عربیکا و روبوستا
            **عربیکا** طعم میوه‌ای‌تر و **روبوستا** کافئین بالاتری دارد.

            ## روش‌های دم‌آوری
            - اسپرسو
            - وی ۶۰
            """;

        var rendered = BlogMarkdownRenderer.Render(markdown);

        Assert.Equal(3, rendered.Toc.Count);
        Assert.Equal(2, rendered.Toc[0].Level);
        Assert.Equal("قهوه چیست؟", rendered.Toc[0].Text);
        Assert.Equal(3, rendered.Toc[1].Level);
        Assert.Contains($"id=\"{rendered.Toc[0].Id}\"", rendered.Html);
        Assert.Contains("<strong>عربیکا</strong>", rendered.Html);
        Assert.Contains("<ul>", rendered.Html);
        Assert.True(rendered.WordCount >= 15);
        Assert.True(rendered.ReadingTimeMinutes >= 1);
    }

    [Fact]
    public void Render_RendersTablesBlockquotesAndCodeSafely()
    {
        const string markdown = """
            > دم‌آوری قهوه تخصصی نیازمند دمای ۹۲ تا ۹۴ درجه است.

            | روش | نسبت |
            | --- | --- |
            | وی ۶۰ | ۱ به ۱۶ |
            | فرنچ پرس | ۱ به ۱۵ |
            """;

        var rendered = BlogMarkdownRenderer.Render(markdown);

        Assert.Contains("<blockquote>", rendered.Html);
        Assert.Contains("<div class=\"mag-table-wrap\"><table>", rendered.Html);
        Assert.Contains("<th>روش</th>", rendered.Html);
        Assert.Contains("<td>وی ۶۰</td>", rendered.Html);
    }

    [Fact]
    public void Render_EscapesRawHtmlAndBlocksJavascriptUrls()
    {
        const string markdown = """
            <script>alert('xss')</script>
            [کلیک کن](javascript:alert(1))
            [منوی کافه](/)
            ![فنجان قهوه](/api/files/coffee.jpg)
            """;

        var rendered = BlogMarkdownRenderer.Render(markdown);

        Assert.DoesNotContain("<script>", rendered.Html);
        Assert.Contains("&lt;script&gt;", rendered.Html);
        Assert.DoesNotContain("javascript:", rendered.Html);
        Assert.Contains("<a href=\"/\">منوی کافه</a>", rendered.Html);
        Assert.Contains("<img src=\"/api/files/coffee.jpg\" alt=\"فنجان قهوه\" loading=\"lazy\" decoding=\"async\" />", rendered.Html);
    }

    [Fact]
    public void ParseFaqItems_ParsesValidJsonAndIgnoresInvalidItems()
    {
        const string json = """
            [
              { "question": "بهترین قهوه برای اسپرسو چیست؟", "answer": "ترکیب ۷۰ درصد عربیکا و ۳۰ درصد روبوستا." },
              { "question": "   ", "answer": "باید حذف شود" }
            ]
            """;

        var faqs = BlogMarkdownRenderer.ParseFaqItems(json);

        Assert.Single(faqs);
        Assert.Equal("بهترین قهوه برای اسپرسو چیست؟", faqs[0].Question);
        Assert.Equal("ترکیب ۷۰ درصد عربیکا و ۳۰ درصد روبوستا.", faqs[0].Answer);
        Assert.Empty(BlogMarkdownRenderer.ParseFaqItems("{not-valid-json"));
    }

    [Fact]
    public void EstimateReadingTimeMinutes_CalculatesBasedOnWordCount()
    {
        var shortText = "قهوه تازه";
        var longText = string.Join(' ', Enumerable.Repeat("قهوه", 420));

        Assert.Equal(1, BlogMarkdownRenderer.EstimateReadingTimeMinutes(shortText));
        Assert.Equal(3, BlogMarkdownRenderer.EstimateReadingTimeMinutes(longText));
    }
}
