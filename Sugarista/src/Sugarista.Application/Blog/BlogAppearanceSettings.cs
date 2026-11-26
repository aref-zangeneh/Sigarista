using System.Text.Json;
using System.Text.RegularExpressions;

namespace Sugarista.Application.Blog;

public interface IMagCacheService
{
    long Version { get; }
    void InvalidateAll();
}

public sealed record BlogCustomNavLink(
    string Label,
    string Url,
    string Placement = "header", // "header" | "footer" | "both"
    int DisplayOrder = 0);

public sealed record BlogAppearanceSettings
{
    // 1. Brand Color Palette
    public string PrimaryDeepColor { get; init; } = "#0C344F";
    public string SecondaryGoldColor { get; init; } = "#E5AA2F";
    public string AccentMagentaColor { get; init; } = "#AA3171";
    public string PageBackgroundColor { get; init; } = "#FAF8F3";
    public string DefaultColorMode { get; init; } = "light"; // "light" | "dark"
    public bool ShowThemeToggle { get; init; } = true;

    // 2. Header & Masthead Branding
    public string BrandMonogram { get; init; } = "S";
    public string? BrandLogoImageUrl { get; init; }
    public string HeaderBrandTitle { get; init; } = "Sugarista";
    public string HeaderBrandSubtitle { get; init; } = "کافه و قنادی تخصصی شوگاریستا";
    public string HeaderHomeLinkLabel { get; init; } = "صفحه اول مجله";
    public string HeaderSearchPlaceholder { get; init; } = "جستجو در مقالات، قهوه، شیرینی...";
    public string HeaderCtaLabel { get; init; } = "مشاهده منوی کافه";
    public string HeaderCtaUrl { get; init; } = "/";

    // 3. Main Hero Banner (/mag)
    public string HeroBadgeText { get; init; } = "داستان دانه‌های قهوه، شیرینی‌های دست‌ساز و لحظه‌های شیرین";
    public string HeroTitlePrefix { get; init; } = "مجله طعم و زندگی";
    public string HeroTitleHighlight { get; init; } = "شوگاریستا";
    public string HeroTitleSuffix { get; init; } = string.Empty;
    public string HeroSubtitle { get; init; } = "اینجا درباره رازهای عصاره‌گیری اسپرسوی بی‌نقص، هنر قنادی، داستان‌های پشت بار کافه و سبک زندگی کافه‌نشینی می‌خوانید.";
    public string HeroPrimaryButtonLabel { get; init; } = "شروع مطالعه مقالات";
    public string HeroPrimaryButtonUrl { get; init; } = "#mag-articles";
    public string HeroSecondaryButtonLabel { get; init; } = "سفارش آنلاین از منو";
    public string HeroSecondaryButtonUrl { get; init; } = "/";
    public bool ShowHeroStats { get; init; } = true;

    // 4. Category Rail, Featured Card, Highlights Strip & Archive Grid
    public bool ShowCategoryRail { get; init; } = true;
    public string AllCategoriesChipLabel { get; init; } = "همه مطالب";
    public bool ShowFeaturedSection { get; init; } = true;
    public string FeaturedBadgeLabel { get; init; } = "پیشنهاد سرآشپز و باریستا";
    public string FeaturedReadButtonLabel { get; init; } = "مطالعه کامل مقاله";
    public bool ShowHighlightsSection { get; init; } = true;
    public string HighlightsSectionEyebrow { get; init; } = "پشت بار شوگاریستا";
    public string HighlightsSectionTitle { get; init; } = "نکات طلایی قهوه و شیرینی";
    public string ArchiveSectionEyebrow { get; init; } = "تازه‌ترین نوشته‌ها";
    public string ArchiveSectionTitle { get; init; } = "آرشیو مقالات مجله";
    public int PostsPerPage { get; init; } = 9;
    public string CardReadMoreLabel { get; init; } = "ادامه مطالعه";

    // 5. Single Article Page (/mag/{slug})
    public bool ShowReadingProgressBar { get; init; } = true;
    public bool ShowTableOfContents { get; init; } = true;
    public string TocTitle { get; init; } = "آنچه در این مقاله می‌خوانید";
    public bool ShowViewCount { get; init; } = true;
    public bool ShowShareButtons { get; init; } = true;
    public string RelatedMenuEyebrow { get; init; } = "پیشنهاد خوشمزه مرتبط با این مطلب";
    public string RelatedMenuTitle { get; init; } = "همین طعم را از منوی کافه سفارش دهید";
    public string FaqSectionTitle { get; init; } = "سوالات پرتکرار شما";
    public string RelatedPostsTitle { get; init; } = "مطالب مشابه که دوست خواهید داشت";

    // 6. Cafe Menu Conversion Banner
    public bool ShowCafeCtaBanner { get; init; } = true;
    public string CafeCtaEyebrow { get; init; } = "تجربه حضوری و سفارش آنلاین";
    public string CafeCtaTitle { get; init; } = "هوس یک اسپرسوی تازه یا شیرینی دست‌ساز کردید؟";
    public string CafeCtaDescription { get; init; } = "در منوی دیجیتال شوگاریستا جدیدترین نوشیدنی‌های تخصصی، کیک‌ها و دسرهای پخت روز را ببینید و همین الان آنلاین سفارش دهید.";
    public string CafeCtaButtonLabel { get; init; } = "ورود به منوی دیجیتال و سفارش";
    public string CafeCtaButtonUrl { get; init; } = "/";
    public string? CafeCtaImageUrl { get; init; }

    // 7. Footer & Base SEO
    public string FooterDescription { get; init; } = "مجله تخصصی طعم، قهوه و قنادی شوگاریستا؛ جایی برای کشف داستان دانه‌ها، رسپی‌های دقیق و لحظه‌های شیرین کافه‌نشینی.";
    public string FooterCategoriesHeading { get; init; } = "موضوعات مجله";
    public string FooterQuickLinksHeading { get; init; } = "لینک‌های کاربردی";
    public string FooterCopyrightText { get; init; } = "تمامی حقوق برای کافه و قنادی تخصصی شوگاریستا محفوظ است.";
    public string HomeMetaTitle { get; init; } = string.Empty;
    public string HomeMetaDescription { get; init; } = string.Empty;

    // 8. Custom Header / Footer Links
    public IReadOnlyList<BlogCustomNavLink> CustomLinks { get; init; } = [];
}

public static partial class BlogAppearanceResolver
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [GeneratedRegex(@"^#[0-9A-Fa-f]{6}$")]
    private static partial Regex HexColorRegex();

    public static BlogAppearanceSettings CreateDefaults(string? cafeName = null)
    {
        var defaults = new BlogAppearanceSettings();
        if (string.IsNullOrWhiteSpace(cafeName))
        {
            return defaults;
        }

        return defaults;
    }

    public static BlogAppearanceSettings Resolve(string? json, string? cafeName = null)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return CreateDefaults(cafeName);
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<BlogAppearanceSettings>(json, JsonOptions);
            return parsed is null ? CreateDefaults(cafeName) : Sanitize(parsed);
        }
        catch
        {
            return CreateDefaults(cafeName);
        }
    }

    public static string Serialize(BlogAppearanceSettings settings)
    {
        var clean = Sanitize(settings);
        return JsonSerializer.Serialize(clean, JsonOptions);
    }

    public static BlogAppearanceSettings Sanitize(BlogAppearanceSettings raw)
    {
        var def = new BlogAppearanceSettings();

        var customLinks = (raw.CustomLinks ?? [])
            .Where(l => !string.IsNullOrWhiteSpace(l.Label) && SanitizeNavUrl(l.Url) is not null)
            .Select(l => new BlogCustomNavLink(
                TrimMax(l.Label, 60, "لینک"),
                SanitizeNavUrl(l.Url)!,
                NormalizePlacement(l.Placement),
                Math.Max(0, l.DisplayOrder)))
            .OrderBy(l => l.DisplayOrder)
            .Take(16)
            .ToList();

        return new BlogAppearanceSettings
        {
            PrimaryDeepColor = NormalizeHex(raw.PrimaryDeepColor, def.PrimaryDeepColor),
            SecondaryGoldColor = NormalizeHex(raw.SecondaryGoldColor, def.SecondaryGoldColor),
            AccentMagentaColor = NormalizeHex(raw.AccentMagentaColor, def.AccentMagentaColor),
            PageBackgroundColor = NormalizeHex(raw.PageBackgroundColor, def.PageBackgroundColor),
            DefaultColorMode = string.Equals(raw.DefaultColorMode, "dark", StringComparison.OrdinalIgnoreCase) ? "dark" : "light",
            ShowThemeToggle = raw.ShowThemeToggle,

            BrandMonogram = TrimMax(raw.BrandMonogram, 4, def.BrandMonogram),
            BrandLogoImageUrl = SanitizeOptionalUrl(raw.BrandLogoImageUrl),
            HeaderBrandTitle = TrimMax(raw.HeaderBrandTitle, 80, def.HeaderBrandTitle),
            HeaderBrandSubtitle = TrimAllowEmpty(raw.HeaderBrandSubtitle, 140),
            HeaderHomeLinkLabel = TrimMax(raw.HeaderHomeLinkLabel, 50, def.HeaderHomeLinkLabel),
            HeaderSearchPlaceholder = TrimMax(raw.HeaderSearchPlaceholder, 90, def.HeaderSearchPlaceholder),
            HeaderCtaLabel = TrimMax(raw.HeaderCtaLabel, 50, def.HeaderCtaLabel),
            HeaderCtaUrl = SanitizeNavUrl(raw.HeaderCtaUrl) ?? def.HeaderCtaUrl,

            HeroBadgeText = TrimAllowEmpty(raw.HeroBadgeText, 160),
            HeroTitlePrefix = TrimMax(raw.HeroTitlePrefix, 120, def.HeroTitlePrefix),
            HeroTitleHighlight = TrimAllowEmpty(raw.HeroTitleHighlight, 80),
            HeroTitleSuffix = TrimAllowEmpty(raw.HeroTitleSuffix, 100),
            HeroSubtitle = TrimAllowEmpty(raw.HeroSubtitle, 400),
            HeroPrimaryButtonLabel = TrimAllowEmpty(raw.HeroPrimaryButtonLabel, 60),
            HeroPrimaryButtonUrl = SanitizeNavUrl(raw.HeroPrimaryButtonUrl) ?? def.HeroPrimaryButtonUrl,
            HeroSecondaryButtonLabel = TrimAllowEmpty(raw.HeroSecondaryButtonLabel, 60),
            HeroSecondaryButtonUrl = SanitizeNavUrl(raw.HeroSecondaryButtonUrl) ?? def.HeroSecondaryButtonUrl,
            ShowHeroStats = raw.ShowHeroStats,

            ShowCategoryRail = raw.ShowCategoryRail,
            AllCategoriesChipLabel = TrimMax(raw.AllCategoriesChipLabel, 50, def.AllCategoriesChipLabel),
            ShowFeaturedSection = raw.ShowFeaturedSection,
            FeaturedBadgeLabel = TrimMax(raw.FeaturedBadgeLabel, 60, def.FeaturedBadgeLabel),
            FeaturedReadButtonLabel = TrimMax(raw.FeaturedReadButtonLabel, 60, def.FeaturedReadButtonLabel),
            ShowHighlightsSection = raw.ShowHighlightsSection,
            HighlightsSectionEyebrow = TrimAllowEmpty(raw.HighlightsSectionEyebrow, 80),
            HighlightsSectionTitle = TrimMax(raw.HighlightsSectionTitle, 120, def.HighlightsSectionTitle),
            ArchiveSectionEyebrow = TrimAllowEmpty(raw.ArchiveSectionEyebrow, 80),
            ArchiveSectionTitle = TrimMax(raw.ArchiveSectionTitle, 120, def.ArchiveSectionTitle),
            PostsPerPage = Math.Clamp(raw.PostsPerPage <= 0 ? def.PostsPerPage : raw.PostsPerPage, 3, 24),
            CardReadMoreLabel = TrimMax(raw.CardReadMoreLabel, 50, def.CardReadMoreLabel),

            ShowReadingProgressBar = raw.ShowReadingProgressBar,
            ShowTableOfContents = raw.ShowTableOfContents,
            TocTitle = TrimMax(raw.TocTitle, 90, def.TocTitle),
            ShowViewCount = raw.ShowViewCount,
            ShowShareButtons = raw.ShowShareButtons,
            RelatedMenuEyebrow = TrimAllowEmpty(raw.RelatedMenuEyebrow, 80),
            RelatedMenuTitle = TrimMax(raw.RelatedMenuTitle, 120, def.RelatedMenuTitle),
            FaqSectionTitle = TrimMax(raw.FaqSectionTitle, 100, def.FaqSectionTitle),
            RelatedPostsTitle = TrimMax(raw.RelatedPostsTitle, 120, def.RelatedPostsTitle),

            ShowCafeCtaBanner = raw.ShowCafeCtaBanner,
            CafeCtaEyebrow = TrimAllowEmpty(raw.CafeCtaEyebrow, 90),
            CafeCtaTitle = TrimMax(raw.CafeCtaTitle, 160, def.CafeCtaTitle),
            CafeCtaDescription = TrimAllowEmpty(raw.CafeCtaDescription, 360),
            CafeCtaButtonLabel = TrimMax(raw.CafeCtaButtonLabel, 60, def.CafeCtaButtonLabel),
            CafeCtaButtonUrl = SanitizeNavUrl(raw.CafeCtaButtonUrl) ?? def.CafeCtaButtonUrl,
            CafeCtaImageUrl = SanitizeOptionalUrl(raw.CafeCtaImageUrl),

            FooterDescription = TrimAllowEmpty(raw.FooterDescription, 360),
            FooterCategoriesHeading = TrimMax(raw.FooterCategoriesHeading, 70, def.FooterCategoriesHeading),
            FooterQuickLinksHeading = TrimMax(raw.FooterQuickLinksHeading, 70, def.FooterQuickLinksHeading),
            FooterCopyrightText = TrimAllowEmpty(raw.FooterCopyrightText, 180),
            HomeMetaTitle = TrimAllowEmpty(raw.HomeMetaTitle, 160),
            HomeMetaDescription = TrimAllowEmpty(raw.HomeMetaDescription, 320),

            CustomLinks = customLinks
        };
    }

    public static string NormalizeHex(string? input, string fallback)
    {
        if (string.IsNullOrWhiteSpace(input)) return fallback;
        var trimmed = input.Trim();
        return HexColorRegex().IsMatch(trimmed) ? trimmed.ToUpperInvariant() : fallback;
    }

    public static string? SanitizeNavUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        var trimmed = url.Trim();
        if (trimmed.StartsWith('#'))
        {
            return trimmed.Length <= 80 && !trimmed.Contains('<') && !trimmed.Contains('"') && !trimmed.Contains('\'')
                ? trimmed
                : null;
        }
        return BlogMarkdownRenderer.SanitizeUrl(trimmed);
    }

    private static string? SanitizeOptionalUrl(string? url) =>
        BlogMarkdownRenderer.SanitizeUrl(url);

    private static string NormalizePlacement(string? placement)
    {
        if (string.Equals(placement, "footer", StringComparison.OrdinalIgnoreCase)) return "footer";
        if (string.Equals(placement, "both", StringComparison.OrdinalIgnoreCase)) return "both";
        return "header";
    }

    private static string TrimMax(string? value, int maxLen, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        var t = value.Trim();
        return t.Length <= maxLen ? t : t[..maxLen].Trim();
    }

    private static string TrimAllowEmpty(string? value, int maxLen)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var t = value.Trim();
        return t.Length <= maxLen ? t : t[..maxLen].Trim();
    }
}
