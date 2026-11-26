using Sugarista.Api.Mag;
using Sugarista.Application.Blog;

namespace Sugarista.Infrastructure.UnitTests.Mag;

public class MagHtmlRendererTests
{
    private static MagSiteContext Site(BlogAppearanceSettings? appearance = null, IReadOnlyList<MagHighlightCard>? highlights = null) =>
        new(
            "https://sugarista.ir",
            "شوگاریستا",
            "کافه تخصصی",
            "09120000000",
            "تهران",
            null,
            [new MagCategorySummary(Guid.NewGuid(), "دم‌آوری", "brewing", "", null, null, "#0C344F", 2)],
            appearance,
            highlights,
            2);

    private static MagPostCard Post(string slug = "espresso", bool featured = true) =>
        new(Guid.NewGuid(), "راهنمای اسپرسو", slug, "خلاصه", null, null, "تحریریه", "دم‌آوری", "brewing", "#0C344F",
            ["اسپرسو"], DateTimeOffset.UtcNow.AddDays(-1), null, 5, 10, featured);

    [Fact]
    public void IndexPage_UsesDefaultSugaristaPaletteAndHeroCopy()
    {
        var html = MagHtmlRenderer.RenderIndexPage(Site(), [Post()], Post(), null, null, null, 1, 1, 1);

        Assert.Contains("--mag-deep:#0C344F", html);
        Assert.Contains("--mag-gold:#E5AA2F", html);
        Assert.Contains("--mag-magenta:#AA3171", html);
        Assert.Contains("mag-hero-highlight\">شوگاریستا", html);
        Assert.Contains("\"@type\":\"Blog\"", html);
        Assert.Contains("id=\"mag-mobile-drawer\"", html);
    }

    [Fact]
    public void IndexPage_ReflectsAdminAppearanceChangesAndToggles()
    {
        var custom = BlogAppearanceResolver.Sanitize(new BlogAppearanceSettings
        {
            PrimaryDeepColor = "#112233",
            HeroTitlePrefix = "عنوان سفارشی",
            HeroTitleHighlight = "طلایی",
            ShowCafeCtaBanner = false,
            ShowThemeToggle = false,
            CustomLinks = [new BlogCustomNavLink("باشگاه", "/club", "header", 1)]
        });

        var html = MagHtmlRenderer.RenderIndexPage(Site(custom), [Post()], Post(), null, null, null, 1, 1, 1);

        Assert.Contains("--mag-deep:#112233", html);
        Assert.Contains("عنوان سفارشی", html);
        Assert.Contains("href=\"/club\"", html);
        Assert.DoesNotContain("class=\"mag-cafe-cta\"", html);
        Assert.DoesNotContain("id=\"mag-theme-btn\"", html);
    }

    [Fact]
    public void IndexPage_RendersActiveHighlightCards()
    {
        var hl = new MagHighlightCard(Guid.NewGuid(), "راز کرما", "توضیح", "نکته", "☕", null, "/mag", "بیشتر", "magenta");
        var html = MagHtmlRenderer.RenderIndexPage(Site(highlights: [hl]), [Post()], Post(), null, null, null, 1, 1, 1);

        Assert.Contains("mag-hl-card is-magenta", html);
        Assert.Contains("راز کرما", html);
    }

    [Fact]
    public void SearchPage_IsNoIndexAndEscapesInput()
    {
        var html = MagHtmlRenderer.RenderIndexPage(Site(), [], null, "<script>x</script>", null, null, 1, 1, 0);

        Assert.Contains("noindex, follow", html);
        Assert.DoesNotContain("<script>x</script>", html);
    }

    [Fact]
    public void ArticlePage_RespectsTocAndShareToggles()
    {
        var rendered = BlogMarkdownRenderer.Render("## الف\nمتن\n\n## ب\nمتن");
        var vm = new MagArticleViewModel(Post(), rendered, [new BlogFaqItem("سوال؟", "جواب")], [], [], null, null, null, null, false);

        var on = MagHtmlRenderer.RenderArticlePage(Site(), vm);
        Assert.Contains("has-toc", on);
        Assert.Contains("mag-share-bar", on);
        Assert.Contains("\"@type\":\"FAQPage\"", on);

        var offAppearance = BlogAppearanceResolver.Sanitize(new BlogAppearanceSettings
        {
            ShowTableOfContents = false,
            ShowShareButtons = false,
            ShowReadingProgressBar = false
        });
        var off = MagHtmlRenderer.RenderArticlePage(Site(offAppearance), vm);
        Assert.DoesNotContain("class=\"mag-article-layout has-toc\"", off);
        Assert.DoesNotContain("class=\"mag-share-bar\"", off);
        Assert.DoesNotContain("id=\"mag-progress\"", off);
    }
}
