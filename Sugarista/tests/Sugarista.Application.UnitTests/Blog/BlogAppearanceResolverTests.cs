using Sugarista.Application.Blog;

namespace Sugarista.Application.UnitTests.Blog;

public class BlogAppearanceResolverTests
{
    [Fact]
    public void CreateDefaults_UsesSignatureSugaristaPaletteAndCopy()
    {
        var defaults = BlogAppearanceResolver.CreateDefaults();

        Assert.Equal("#0C344F", defaults.PrimaryDeepColor);
        Assert.Equal("#E5AA2F", defaults.SecondaryGoldColor);
        Assert.Equal("#AA3171", defaults.AccentMagentaColor);
        Assert.Equal("#FAF8F3", defaults.PageBackgroundColor);
        Assert.Equal("light", defaults.DefaultColorMode);
        Assert.Equal("S", defaults.BrandMonogram);
        Assert.Equal("شوگاریستا", defaults.HeroTitleHighlight);
        Assert.Equal(9, defaults.PostsPerPage);
    }

    [Fact]
    public void SerializeAndResolve_RoundTripsAllDynamicFieldsAndCustomLinks()
    {
        var custom = new BlogAppearanceSettings
        {
            PrimaryDeepColor = "#102a43",
            SecondaryGoldColor = "#f0b429",
            AccentMagentaColor = "#c81e68",
            PageBackgroundColor = "#fffdf8",
            DefaultColorMode = "dark",
            ShowThemeToggle = false,
            BrandMonogram = "ش",
            HeaderBrandTitle = "شوگاریستا پلاس",
            HeaderBrandSubtitle = "کافه تخصصی قهوه و دسر",
            HeroBadgeText = "خوش آمدید به مجله تخصصی",
            HeroTitlePrefix = "مجله اختصاصی",
            HeroTitleHighlight = "قهوه و شیرینی",
            HeroPrimaryButtonLabel = "مشاهده مطالب",
            HeroPrimaryButtonUrl = "#mag-articles",
            PostsPerPage = 12,
            ShowFeaturedSection = false,
            ShowHighlightsSection = true,
            ShowCafeCtaBanner = false,
            CustomLinks =
            [
                new BlogCustomNavLink("باشگاه مشتریان", "/#club", "both", 1),
                new BlogCustomNavLink("اینستاگرام", "https://instagram.com/sugarista", "footer", 2)
            ]
        };

        var json = BlogAppearanceResolver.Serialize(custom);
        var resolved = BlogAppearanceResolver.Resolve(json);

        Assert.Equal("#102A43", resolved.PrimaryDeepColor);
        Assert.Equal("#F0B429", resolved.SecondaryGoldColor);
        Assert.Equal("#C81E68", resolved.AccentMagentaColor);
        Assert.Equal("#FFFDF8", resolved.PageBackgroundColor);
        Assert.Equal("dark", resolved.DefaultColorMode);
        Assert.False(resolved.ShowThemeToggle);
        Assert.Equal("ش", resolved.BrandMonogram);
        Assert.Equal("شوگاریستا پلاس", resolved.HeaderBrandTitle);
        Assert.Equal("قهوه و شیرینی", resolved.HeroTitleHighlight);
        Assert.Equal(12, resolved.PostsPerPage);
        Assert.False(resolved.ShowFeaturedSection);
        Assert.False(resolved.ShowCafeCtaBanner);
        Assert.Equal(2, resolved.CustomLinks.Count);
        Assert.Equal("both", resolved.CustomLinks[0].Placement);
        Assert.Equal("footer", resolved.CustomLinks[1].Placement);
    }

    [Fact]
    public void Sanitize_BlocksCssInjectionColorsJavascriptUrlsAndClampsPageSize()
    {
        var unsafeInput = new BlogAppearanceSettings
        {
            PrimaryDeepColor = "#0C344F;body{display:none}",
            SecondaryGoldColor = "rgb(1,2,3)",
            HeaderCtaUrl = "javascript:alert(1)",
            HeroPrimaryButtonUrl = "javascript:evil()",
            BrandLogoImageUrl = "javascript:alert(2)",
            PostsPerPage = 999,
            CustomLinks =
            [
                new BlogCustomNavLink("مخرب", "javascript:alert(3)", "header", 0),
                new BlogCustomNavLink("سالم", "/menu", "header", 1)
            ]
        };

        var clean = BlogAppearanceResolver.Sanitize(unsafeInput);

        Assert.Equal("#0C344F", clean.PrimaryDeepColor);
        Assert.Equal("#E5AA2F", clean.SecondaryGoldColor);
        Assert.Equal("/", clean.HeaderCtaUrl);
        Assert.Equal("#mag-articles", clean.HeroPrimaryButtonUrl);
        Assert.Null(clean.BrandLogoImageUrl);
        Assert.Equal(24, clean.PostsPerPage);
        Assert.Single(clean.CustomLinks);
        Assert.Equal("سالم", clean.CustomLinks[0].Label);
    }

    [Fact]
    public void Resolve_ReturnsDefaults_WhenJsonIsNullOrMalformed()
    {
        Assert.Equal("#0C344F", BlogAppearanceResolver.Resolve(null).PrimaryDeepColor);
        Assert.Equal("#0C344F", BlogAppearanceResolver.Resolve("{bad json").PrimaryDeepColor);
    }
}
