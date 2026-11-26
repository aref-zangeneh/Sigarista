using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Sugarista.Application.Blog;

namespace Sugarista.Api.Mag;

public sealed record MagHighlightCard(
    Guid Id,
    string Title,
    string Subtitle,
    string BadgeText,
    string IconText,
    string? ImageUrl,
    string? LinkUrl,
    string? LinkLabel,
    string ThemeStyle);

public sealed record MagSiteContext(
    string BaseUrl,
    string CafeName,
    string CafeDescription,
    string? ContactPhone,
    string? Address,
    string? InstagramUrl,
    IReadOnlyList<MagCategorySummary> Categories,
    BlogAppearanceSettings? CustomAppearance = null,
    IReadOnlyList<MagHighlightCard>? Highlights = null,
    int TotalPublishedPosts = 0)
{
    public BlogAppearanceSettings Appearance => CustomAppearance ?? BlogAppearanceResolver.CreateDefaults(CafeName);
    public IReadOnlyList<MagHighlightCard> ActiveHighlights => Highlights ?? [];
}

public sealed record MagCategorySummary(
    Guid Id,
    string Name,
    string Slug,
    string Description,
    string? MetaTitle,
    string? MetaDescription,
    string? AccentColor,
    int PostCount);

public sealed record MagPostCard(
    Guid Id,
    string Title,
    string Slug,
    string Excerpt,
    string? CoverImageUrl,
    string? CoverImageAlt,
    string AuthorName,
    string? CategoryName,
    string? CategorySlug,
    string? CategoryAccent,
    IReadOnlyList<string> Tags,
    DateTimeOffset PublishedAt,
    DateTimeOffset? UpdatedAt,
    int ReadingTimeMinutes,
    long ViewCount,
    bool IsFeatured);

public sealed record MagMenuItemCard(
    Guid Id,
    string PersianName,
    string EnglishName,
    string? ImageUrl,
    decimal Price,
    string? CategoryName);

public sealed record MagArticleViewModel(
    MagPostCard Post,
    BlogRenderedContent Rendered,
    IReadOnlyList<BlogFaqItem> FaqItems,
    IReadOnlyList<MagPostCard> RelatedPosts,
    IReadOnlyList<MagMenuItemCard> RelatedMenuItems,
    string? MetaTitle,
    string? MetaDescription,
    string? FocusKeyword,
    string? CanonicalUrl,
    bool NoIndex);

public static class MagHtmlRenderer
{
    private static readonly PersianCalendar PersianCal = new();
    private static readonly string[] PersianMonths =
    [
        "", "فروردین", "اردیبهشت", "خرداد", "تیر", "مرداد", "شهریور",
        "مهر", "آبان", "آذر", "دی", "بهمن", "اسفند"
    ];

    private static readonly JsonSerializerOptions JsonLdOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public static string ComputeETag(string content)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return $"\"{Convert.ToHexString(hash)[..16]}\"";
    }

    public static string ToPersianDigits(object? value)
    {
        var raw = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        if (raw.Length == 0) return raw;
        var chars = raw.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (chars[i] is >= '0' and <= '9')
            {
                chars[i] = (char)('\u06F0' + (chars[i] - '0'));
            }
        }
        return new string(chars);
    }

    public static string FormatShamsiDate(DateTimeOffset utcDate)
    {
        var tehran = utcDate.ToOffset(TimeSpan.FromHours(3.5)).DateTime;
        var y = PersianCal.GetYear(tehran);
        var m = PersianCal.GetMonth(tehran);
        var d = PersianCal.GetDayOfMonth(tehran);
        var monthName = m is >= 1 and <= 12 ? PersianMonths[m] : m.ToString(CultureInfo.InvariantCulture);
        return $"{ToPersianDigits(d)} {monthName} {ToPersianDigits(y)}";
    }

    public static string FormatMoney(decimal amount) =>
        ToPersianDigits(Math.Round(amount).ToString("N0", CultureInfo.InvariantCulture).Replace(",", "٬"));

    public static string RenderIndexPage(
        MagSiteContext site,
        IReadOnlyList<MagPostCard> posts,
        MagPostCard? featuredPost,
        string? searchQuery,
        MagCategorySummary? activeCategory,
        string? activeTag,
        int page,
        int totalPages,
        int totalCount)
    {
        var ui = site.Appearance;
        var cafe = string.IsNullOrWhiteSpace(site.CafeName) ? "شوگاریستا" : site.CafeName;
        var isMainHome = activeCategory is null &&
                         string.IsNullOrWhiteSpace(activeTag) &&
                         string.IsNullOrWhiteSpace(searchQuery) &&
                         page == 1;

        string pageTitle;
        string metaDesc;
        string canonicalPath;
        string filterHeadingTitle = string.Empty;
        string filterHeadingSub = string.Empty;

        if (activeCategory is not null)
        {
            pageTitle = activeCategory.MetaTitle ?? $"{activeCategory.Name} | مجله {cafe}";
            metaDesc = activeCategory.MetaDescription ?? (string.IsNullOrWhiteSpace(activeCategory.Description)
                ? $"جدیدترین مقالات تخصصی {activeCategory.Name} در مجله {cafe}."
                : activeCategory.Description);
            canonicalPath = $"/mag/category/{Uri.EscapeDataString(activeCategory.Slug)}";
            filterHeadingTitle = activeCategory.Name;
            filterHeadingSub = activeCategory.Description;
        }
        else if (!string.IsNullOrWhiteSpace(activeTag))
        {
            pageTitle = $"مطالب با برچسب «{activeTag}» | مجله {cafe}";
            metaDesc = $"مجموعه مقالات تخصصی درباره {activeTag} در مجله {cafe}.";
            canonicalPath = $"/mag/tag/{Uri.EscapeDataString(activeTag)}";
            filterHeadingTitle = $"برچسب: #{activeTag}";
            filterHeadingSub = $"تمام نوشته‌های مرتبط با برچسب «{activeTag}» در مجله {cafe}.";
        }
        else if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            pageTitle = $"جستجو برای «{searchQuery}» | مجله {cafe}";
            metaDesc = $"نتایج جستجو برای {searchQuery} در مجله {cafe}.";
            canonicalPath = "/mag";
            filterHeadingTitle = $"نتایج جستجو برای «{searchQuery}»";
            filterHeadingSub = $"{ToPersianDigits(totalCount)} مقاله مرتبط یافت شد.";
        }
        else
        {
            var fullHeroHeading = $"{ui.HeroTitlePrefix} {ui.HeroTitleHighlight} {ui.HeroTitleSuffix}".Trim();
            pageTitle = !string.IsNullOrWhiteSpace(ui.HomeMetaTitle)
                ? ui.HomeMetaTitle
                : $"{fullHeroHeading} | راهنمای تخصصی قهوه، قنادی و کافه";
            metaDesc = !string.IsNullOrWhiteSpace(ui.HomeMetaDescription)
                ? ui.HomeMetaDescription
                : (!string.IsNullOrWhiteSpace(ui.HeroSubtitle)
                    ? ui.HeroSubtitle
                    : $"مجله تخصصی {cafe}؛ آموزش قهوه، شیرینی‌پزی و سبک زندگی کافه‌نشینی.");
            canonicalPath = "/mag";
        }

        var canonicalUrl = page > 1 && string.IsNullOrWhiteSpace(searchQuery)
            ? $"{site.BaseUrl}{canonicalPath}?page={page}"
            : $"{site.BaseUrl}{canonicalPath}";

        var paginationLinks = new StringBuilder();
        if (string.IsNullOrWhiteSpace(searchQuery))
        {
            if (page > 1)
            {
                var prevUrl = page == 2 ? $"{site.BaseUrl}{canonicalPath}" : $"{site.BaseUrl}{canonicalPath}?page={page - 1}";
                paginationLinks.Append($"<link rel=\"prev\" href=\"{Enc(prevUrl)}\" />");
            }
            if (page < totalPages)
            {
                var nextUrl = $"{site.BaseUrl}{canonicalPath}?page={page + 1}";
                paginationLinks.Append($"<link rel=\"next\" href=\"{Enc(nextUrl)}\" />");
            }
        }

        var jsonLdBlocks = new List<string>
        {
            JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["@context"] = "https://schema.org",
                ["@type"] = activeCategory is not null || !string.IsNullOrWhiteSpace(activeTag) ? "CollectionPage" : "Blog",
                ["name"] = pageTitle,
                ["description"] = metaDesc,
                ["url"] = canonicalUrl,
                ["inLanguage"] = "fa-IR",
                ["publisher"] = new Dictionary<string, object?>
                {
                    ["@type"] = "CafeOrCoffeeShop",
                    ["name"] = cafe,
                    ["url"] = site.BaseUrl,
                    ["telephone"] = site.ContactPhone,
                    ["address"] = string.IsNullOrWhiteSpace(site.Address) ? null : new Dictionary<string, object?>
                    {
                        ["@type"] = "PostalAddress",
                        ["streetAddress"] = site.Address,
                        ["addressCountry"] = "IR"
                    }
                }
            }, JsonLdOptions),
            JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["@context"] = "https://schema.org",
                ["@type"] = "WebSite",
                ["name"] = $"مجله {cafe}",
                ["url"] = $"{site.BaseUrl}/mag",
                ["potentialAction"] = new Dictionary<string, object?>
                {
                    ["@type"] = "SearchAction",
                    ["target"] = $"{site.BaseUrl}/mag?q={{search_term_string}}",
                    ["query-input"] = "required name=search_term_string"
                }
            }, JsonLdOptions),
            BuildBreadcrumbJsonLd(site, activeCategory, activeTag, null)
        };

        var body = new StringBuilder();
        body.Append(RenderMasthead(site, searchQuery, activeCategory?.Slug));
        body.Append("<main class=\"mag-main\" id=\"content\">");

        if (isMainHome)
        {
            body.Append(RenderHomeHero(site, featuredPost, totalCount));
        }
        else
        {
            body.Append(RenderFilteredHero(site, filterHeadingTitle, filterHeadingSub, activeCategory, totalCount));
        }

        if (ui.ShowCategoryRail && (site.Categories.Count > 0 || !string.IsNullOrWhiteSpace(searchQuery)))
        {
            body.Append(RenderCategoryDock(site, activeCategory, activeTag, searchQuery));
        }

        body.Append("<div class=\"mag-container mag-content-stack\">");

        // Dynamic Highlight / Bento Cards Strip (only on main home page 1 when enabled and items exist)
        if (isMainHome && ui.ShowHighlightsSection && site.ActiveHighlights.Count > 0)
        {
            body.Append(RenderHighlightsSection(ui, site.ActiveHighlights));
        }

        // Featured Editorial Story Banner
        var showFeaturedBanner = isMainHome && ui.ShowFeaturedSection && featuredPost is not null;
        if (showFeaturedBanner && featuredPost is not null)
        {
            body.Append(RenderFeaturedBanner(featuredPost, ui));
        }

        // Articles Grid
        var gridPosts = showFeaturedBanner && featuredPost is not null && posts.Count >= 4
            ? posts.Where(p => p.Id != featuredPost.Id).ToList()
            : posts.ToList();

        body.Append("<section class=\"mag-archive-section\" id=\"mag-articles\" aria-label=\"فهرست مقالات\">");
        if (isMainHome)
        {
            body.Append("<div class=\"mag-section-head\">");
            body.Append("<div>");
            if (!string.IsNullOrWhiteSpace(ui.ArchiveSectionEyebrow))
            {
                body.Append($"<span class=\"mag-eyebrow\">{Enc(ui.ArchiveSectionEyebrow)}</span>");
            }
            body.Append($"<h2>{Enc(ui.ArchiveSectionTitle)}</h2>");
            body.Append("</div>");
            body.Append($"<span class=\"mag-count-pill\">{ToPersianDigits(totalCount)} مقاله</span>");
            body.Append("</div>");
        }

        if (gridPosts.Count == 0 && !showFeaturedBanner)
        {
            body.Append($"""
                <div class="mag-empty">
                  <div class="mag-empty-icon" aria-hidden="true">☕</div>
                  <h3>هنوز مقاله‌ای در این بخش منتشر نشده است</h3>
                  <p>می‌توانید سایر بخش‌های مجله را مرور کنید یا به منوی دیجیتال سر بزنید.</p>
                  <div class="mag-empty-actions">
                    <a class="mag-btn mag-btn--gold" href="/mag">{Enc(ui.AllCategoriesChipLabel)}</a>
                    <a class="mag-btn mag-btn--outline" href="{Enc(ui.HeaderCtaUrl)}">{Enc(ui.HeaderCtaLabel)}</a>
                  </div>
                </div>
                """);
        }
        else if (gridPosts.Count > 0)
        {
            body.Append("<div class=\"mag-grid\">");
            for (var i = 0; i < gridPosts.Count; i++)
            {
                body.Append(RenderPostCard(gridPosts[i], ui, i));
            }
            body.Append("</div>");

            if (totalPages > 1)
            {
                body.Append(RenderPagination(canonicalPath, searchQuery, page, totalPages));
            }
        }
        body.Append("</section>");

        if (ui.ShowCafeCtaBanner)
        {
            body.Append(RenderCafeCtaBanner(ui));
        }

        body.Append("</div></main>");
        body.Append(RenderFooter(site));

        var noIndex = !string.IsNullOrWhiteSpace(searchQuery);
        return RenderShell(
            site,
            pageTitle,
            metaDesc,
            canonicalUrl,
            ogType: "website",
            ogImage: featuredPost?.CoverImageUrl,
            noIndex: noIndex,
            extraHead: paginationLinks.ToString(),
            jsonLdBlocks: jsonLdBlocks,
            bodyHtml: body.ToString(),
            includeArticleScript: false);
    }

    public static string RenderArticlePage(MagSiteContext site, MagArticleViewModel vm)
    {
        var ui = site.Appearance;
        var post = vm.Post;
        var cafe = string.IsNullOrWhiteSpace(site.CafeName) ? "شوگاریستا" : site.CafeName;
        var pageTitle = !string.IsNullOrWhiteSpace(vm.MetaTitle)
            ? vm.MetaTitle
            : $"{post.Title} | مجله {cafe}";
        var metaDesc = !string.IsNullOrWhiteSpace(vm.MetaDescription)
            ? vm.MetaDescription
            : (!string.IsNullOrWhiteSpace(post.Excerpt) ? post.Excerpt : post.Title);
        var articlePath = $"/mag/{Uri.EscapeDataString(post.Slug)}";
        var canonicalUrl = !string.IsNullOrWhiteSpace(vm.CanonicalUrl)
            ? vm.CanonicalUrl
            : $"{site.BaseUrl}{articlePath}";
        var fullCoverUrl = ResolveAbsoluteUrl(site.BaseUrl, post.CoverImageUrl);

        var extraHead = new StringBuilder();
        extraHead.Append($"<meta property=\"article:published_time\" content=\"{post.PublishedAt:O}\" />");
        if (post.UpdatedAt.HasValue)
        {
            extraHead.Append($"<meta property=\"article:modified_time\" content=\"{post.UpdatedAt.Value:O}\" />");
        }
        if (!string.IsNullOrWhiteSpace(post.CategoryName))
        {
            extraHead.Append($"<meta property=\"article:section\" content=\"{Enc(post.CategoryName)}\" />");
        }
        foreach (var tag in post.Tags)
        {
            extraHead.Append($"<meta property=\"article:tag\" content=\"{Enc(tag)}\" />");
        }

        var jsonLdBlocks = new List<string>
        {
            JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["@context"] = "https://schema.org",
                ["@type"] = "BlogPosting",
                ["headline"] = post.Title,
                ["description"] = metaDesc,
                ["url"] = canonicalUrl,
                ["mainEntityOfPage"] = new Dictionary<string, object?>
                {
                    ["@type"] = "WebPage",
                    ["@id"] = canonicalUrl
                },
                ["image"] = fullCoverUrl is not null ? new[] { fullCoverUrl } : null,
                ["datePublished"] = post.PublishedAt.ToString("O"),
                ["dateModified"] = (post.UpdatedAt ?? post.PublishedAt).ToString("O"),
                ["inLanguage"] = "fa-IR",
                ["wordCount"] = vm.Rendered.WordCount > 0 ? vm.Rendered.WordCount : null,
                ["timeRequired"] = $"PT{Math.Max(1, post.ReadingTimeMinutes)}M",
                ["articleSection"] = post.CategoryName,
                ["keywords"] = post.Tags.Count > 0 ? string.Join(", ", post.Tags) : vm.FocusKeyword,
                ["author"] = new Dictionary<string, object?>
                {
                    ["@type"] = "Person",
                    ["name"] = post.AuthorName
                },
                ["publisher"] = new Dictionary<string, object?>
                {
                    ["@type"] = "CafeOrCoffeeShop",
                    ["name"] = cafe,
                    ["url"] = site.BaseUrl
                }
            }, JsonLdOptions),
            BuildBreadcrumbJsonLd(
                site,
                post.CategoryName is not null && post.CategorySlug is not null
                    ? new MagCategorySummary(Guid.Empty, post.CategoryName, post.CategorySlug, "", null, null, null, 0)
                    : null,
                null,
                post)
        };

        if (vm.FaqItems.Count > 0)
        {
            jsonLdBlocks.Add(JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["@context"] = "https://schema.org",
                ["@type"] = "FAQPage",
                ["mainEntity"] = vm.FaqItems.Select(faq => new Dictionary<string, object?>
                {
                    ["@type"] = "Question",
                    ["name"] = faq.Question,
                    ["acceptedAnswer"] = new Dictionary<string, object?>
                    {
                        ["@type"] = "Answer",
                        ["text"] = faq.Answer
                    }
                }).ToArray()
            }, JsonLdOptions));
        }

        var body = new StringBuilder();
        if (ui.ShowReadingProgressBar)
        {
            body.Append("<div class=\"mag-progress-bar\" id=\"mag-progress\" aria-hidden=\"true\"></div>");
        }
        body.Append(RenderMasthead(site, null, post.CategorySlug));

        body.Append("<main class=\"mag-main\" id=\"content\">");
        body.Append("<article class=\"mag-article-shell\" itemscope itemtype=\"https://schema.org/BlogPosting\">");

        // Royal Petrol Article Hero Header
        body.Append("<header class=\"mag-article-hero\">");
        body.Append("<div class=\"mag-hero-orb mag-hero-orb--magenta\" aria-hidden=\"true\"></div>");
        body.Append("<div class=\"mag-hero-orb mag-hero-orb--gold\" aria-hidden=\"true\"></div>");
        body.Append("<div class=\"mag-container mag-article-hero-inner\">");

        body.Append("<nav class=\"mag-breadcrumb is-on-dark\" aria-label=\"مسیر صفحه\">");
        body.Append($"<a href=\"{Enc(ui.HeaderCtaUrl)}\">{Enc(ui.HeaderCtaLabel)}</a><span aria-hidden=\"true\">/</span>");
        body.Append($"<a href=\"/mag\">{Enc(ui.HeaderHomeLinkLabel)}</a><span aria-hidden=\"true\">/</span>");
        if (!string.IsNullOrWhiteSpace(post.CategoryName) && !string.IsNullOrWhiteSpace(post.CategorySlug))
        {
            body.Append($"<a href=\"/mag/category/{Uri.EscapeDataString(post.CategorySlug)}\">{Enc(post.CategoryName)}</a><span aria-hidden=\"true\">/</span>");
        }
        body.Append($"<span class=\"mag-breadcrumb-current\">{Enc(post.Title)}</span>");
        body.Append("</nav>");

        body.Append("<div class=\"mag-article-badges\">");
        if (!string.IsNullOrWhiteSpace(post.CategoryName) && !string.IsNullOrWhiteSpace(post.CategorySlug))
        {
            body.Append($"<a class=\"mag-chip mag-chip--magenta\" href=\"/mag/category/{Uri.EscapeDataString(post.CategorySlug)}\">{Enc(post.CategoryName)}</a>");
        }
        body.Append($"<span class=\"mag-chip mag-chip--gold\">⏱ {ToPersianDigits(Math.Max(1, post.ReadingTimeMinutes))} دقیقه مطالعه</span>");
        body.Append("</div>");

        body.Append($"<h1 class=\"mag-article-title\" itemprop=\"headline\">{Enc(post.Title)}</h1>");
        if (!string.IsNullOrWhiteSpace(post.Excerpt))
        {
            body.Append($"<p class=\"mag-article-lead\" itemprop=\"description\">{Enc(post.Excerpt)}</p>");
        }

        body.Append("<div class=\"mag-meta-glass\">");
        body.Append($"<div class=\"mag-meta-pair\"><span>نویسنده:</span><strong itemprop=\"author\">{Enc(post.AuthorName)}</strong></div>");
        body.Append("<span class=\"mag-meta-dot\" aria-hidden=\"true\">•</span>");
        body.Append($"<div class=\"mag-meta-pair\"><span>انتشار:</span><time datetime=\"{post.PublishedAt:O}\" itemprop=\"datePublished\">{FormatShamsiDate(post.PublishedAt)}</time></div>");
        if (post.UpdatedAt.HasValue && (post.UpdatedAt.Value - post.PublishedAt).TotalHours >= 12)
        {
            body.Append("<span class=\"mag-meta-dot\" aria-hidden=\"true\">•</span>");
            body.Append($"<div class=\"mag-meta-pair\"><span>به‌روزرسانی:</span><time datetime=\"{post.UpdatedAt.Value:O}\">{FormatShamsiDate(post.UpdatedAt.Value)}</time></div>");
        }
        if (ui.ShowViewCount && post.ViewCount > 0)
        {
            body.Append("<span class=\"mag-meta-dot\" aria-hidden=\"true\">•</span>");
            body.Append($"<div class=\"mag-meta-pair\"><span>بازدید:</span><strong>{ToPersianDigits(post.ViewCount)}</strong></div>");
        }
        body.Append("</div>");
        body.Append("</div>");
        body.Append(RenderHeroWaveSvg());
        body.Append("</header>");

        body.Append("<div class=\"mag-container mag-article-body-wrap\">");

        if (!string.IsNullOrWhiteSpace(post.CoverImageUrl))
        {
            body.Append("<figure class=\"mag-article-cover\">");
            body.Append($"<img src=\"{Enc(post.CoverImageUrl)}\" alt=\"{Enc(post.CoverImageAlt ?? post.Title)}\" fetchpriority=\"high\" decoding=\"async\" itemprop=\"image\" />");
            if (!string.IsNullOrWhiteSpace(post.CoverImageAlt))
            {
                body.Append($"<figcaption>{Enc(post.CoverImageAlt)}</figcaption>");
            }
            body.Append("</figure>");
        }

        var hasToc = ui.ShowTableOfContents && vm.Rendered.Toc.Count >= 2;
        body.Append($"<div class=\"mag-article-layout{(hasToc ? " has-toc" : "")}\">");

        if (hasToc)
        {
            body.Append($"<aside class=\"mag-toc-aside\" aria-label=\"{Enc(ui.TocTitle)}\">");
            body.Append("<details class=\"mag-toc-box\" open>");
            body.Append($"<summary><span>📑 {Enc(ui.TocTitle)}</span><span class=\"mag-toc-chevron\" aria-hidden=\"true\">⌄</span></summary>");
            body.Append("<ol class=\"mag-toc-list\">");
            foreach (var item in vm.Rendered.Toc)
            {
                var levelClass = item.Level == 3 ? " class=\"is-sub\"" : string.Empty;
                body.Append($"<li{levelClass}><a href=\"#{Enc(item.Id)}\" data-toc-link=\"{Enc(item.Id)}\">{Enc(item.Text)}</a></li>");
            }
            body.Append("</ol></details></aside>");
        }

        body.Append("<div class=\"mag-article-body-col\">");
        body.Append($"<div class=\"mag-prose-card\"><div class=\"mag-prose\" itemprop=\"articleBody\">{vm.Rendered.Html}</div></div>");

        if (vm.RelatedMenuItems.Count > 0)
        {
            body.Append("<section class=\"mag-menu-showcase\" aria-label=\"پیشنهاد از منوی کافه\">");
            body.Append("<div class=\"mag-section-head\">");
            body.Append("<div>");
            if (!string.IsNullOrWhiteSpace(ui.RelatedMenuEyebrow))
            {
                body.Append($"<span class=\"mag-eyebrow\">{Enc(ui.RelatedMenuEyebrow)}</span>");
            }
            body.Append($"<h2>{Enc(ui.RelatedMenuTitle)}</h2>");
            body.Append("</div>");
            body.Append($"<a class=\"mag-btn mag-btn--gold mag-btn--sm\" href=\"{Enc(ui.HeaderCtaUrl)}\">{Enc(ui.HeaderCtaLabel)} ←</a>");
            body.Append("</div>");
            body.Append("<div class=\"mag-menu-grid\">");
            foreach (var item in vm.RelatedMenuItems)
            {
                body.Append($"<a class=\"mag-menu-card\" href=\"{Enc(ui.HeaderCtaUrl)}\">");
                if (!string.IsNullOrWhiteSpace(item.ImageUrl))
                {
                    body.Append($"<img src=\"{Enc(item.ImageUrl)}\" alt=\"{Enc(item.PersianName)}\" loading=\"lazy\" decoding=\"async\" />");
                }
                else
                {
                    body.Append("<div class=\"mag-menu-card-placeholder\" aria-hidden=\"true\">☕</div>");
                }
                body.Append("<div class=\"mag-menu-card-body\">");
                body.Append($"<strong>{Enc(item.PersianName)}</strong>");
                if (!string.IsNullOrWhiteSpace(item.EnglishName))
                {
                    body.Append($"<span class=\"mag-ltr\">{Enc(item.EnglishName)}</span>");
                }
                if (item.Price > 0)
                {
                    body.Append($"<em>{FormatMoney(item.Price)} تومان</em>");
                }
                body.Append("</div></a>");
            }
            body.Append("</div></section>");
        }

        if (vm.FaqItems.Count > 0)
        {
            body.Append("<section class=\"mag-faq-section\" aria-label=\"سوالات متداول\">");
            body.Append($"<div class=\"mag-section-head\"><h2>{Enc(ui.FaqSectionTitle)}</h2></div>");
            body.Append("<div class=\"mag-faq-list\">");
            foreach (var faq in vm.FaqItems)
            {
                body.Append("<details class=\"mag-faq-item\">");
                body.Append($"<summary><span>{Enc(faq.Question)}</span></summary>");
                body.Append($"<p>{Enc(faq.Answer)}</p>");
                body.Append("</details>");
            }
            body.Append("</div></section>");
        }

        if (post.Tags.Count > 0 || ui.ShowShareButtons)
        {
            body.Append("<div class=\"mag-article-footer\">");
            if (post.Tags.Count > 0)
            {
                body.Append("<div class=\"mag-tags\" aria-label=\"برچسب‌ها\">");
                foreach (var tag in post.Tags)
                {
                    body.Append($"<a class=\"mag-tag\" href=\"/mag/tag/{Uri.EscapeDataString(tag)}\">#{Enc(tag)}</a>");
                }
                body.Append("</div>");
            }

            if (ui.ShowShareButtons)
            {
                var encodedUrl = Uri.EscapeDataString(canonicalUrl);
                var encodedTitle = Uri.EscapeDataString(post.Title);
                body.Append($"""
                    <div class="mag-share-bar">
                      <span>اشتراک‌گذاری این مطلب:</span>
                      <button type="button" class="mag-share-btn" data-copy-url="{Enc(canonicalUrl)}">🔗 کپی لینک</button>
                      <a class="mag-share-btn" href="https://t.me/share/url?url={encodedUrl}&amp;text={encodedTitle}" target="_blank" rel="noopener noreferrer">تلگرام</a>
                      <a class="mag-share-btn" href="https://wa.me/?text={encodedTitle}%20{encodedUrl}" target="_blank" rel="noopener noreferrer">واتساپ</a>
                      <a class="mag-share-btn mag-ltr" href="https://x.com/intent/tweet?url={encodedUrl}&amp;text={encodedTitle}" target="_blank" rel="noopener noreferrer">X</a>
                    </div>
                    """);
            }
            body.Append("</div>");
        }

        body.Append("</div>"); // .mag-article-body-col
        body.Append("</div>"); // .mag-article-layout

        if (vm.RelatedPosts.Count > 0)
        {
            body.Append("<section class=\"mag-related-section\" aria-label=\"مطالب مرتبط\">");
            body.Append($"<div class=\"mag-section-head\"><h2>{Enc(ui.RelatedPostsTitle)}</h2><a class=\"mag-text-link\" href=\"/mag\">{Enc(ui.AllCategoriesChipLabel)} ←</a></div>");
            body.Append("<div class=\"mag-grid\">");
            for (var i = 0; i < vm.RelatedPosts.Count; i++)
            {
                body.Append(RenderPostCard(vm.RelatedPosts[i], ui, i));
            }
            body.Append("</div></section>");
        }

        if (ui.ShowCafeCtaBanner)
        {
            body.Append(RenderCafeCtaBanner(ui));
        }

        body.Append("</div></article></main>");
        body.Append(RenderFooter(site));

        return RenderShell(
            site,
            pageTitle,
            metaDesc,
            canonicalUrl,
            ogType: "article",
            ogImage: post.CoverImageUrl,
            noIndex: vm.NoIndex,
            extraHead: extraHead.ToString(),
            jsonLdBlocks: jsonLdBlocks,
            bodyHtml: body.ToString(),
            includeArticleScript: true);
    }

    public static string RenderSitemapXml(
        string baseUrl,
        IReadOnlyList<(string Slug, DateTimeOffset LastMod)> categories,
        IReadOnlyList<(string Slug, DateTimeOffset LastMod, string? CoverImageUrl, string Title)> posts,
        bool includeBlog = true)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\" xmlns:image=\"http://www.google.com/schemas/sitemap-image/1.1\">");

        sb.AppendLine($"  <url><loc>{Enc(baseUrl)}/</loc><changefreq>daily</changefreq><priority>1.0</priority></url>");

        if (includeBlog)
        {
            var latestMod = posts.Count > 0 ? posts.Max(p => p.LastMod) : DateTimeOffset.UtcNow;
            sb.AppendLine($"  <url><loc>{Enc(baseUrl)}/mag</loc><lastmod>{latestMod:yyyy-MM-dd}</lastmod><changefreq>daily</changefreq><priority>0.95</priority></url>");

            foreach (var cat in categories)
            {
                var loc = $"{baseUrl}/mag/category/{Uri.EscapeDataString(cat.Slug)}";
                sb.AppendLine($"  <url><loc>{Enc(loc)}</loc><lastmod>{cat.LastMod:yyyy-MM-dd}</lastmod><changefreq>weekly</changefreq><priority>0.8</priority></url>");
            }

            foreach (var post in posts)
            {
                var loc = $"{baseUrl}/mag/{Uri.EscapeDataString(post.Slug)}";
                sb.Append($"  <url><loc>{Enc(loc)}</loc><lastmod>{post.LastMod:yyyy-MM-dd}</lastmod><changefreq>weekly</changefreq><priority>0.9</priority>");
                var img = ResolveAbsoluteUrl(baseUrl, post.CoverImageUrl);
                if (img is not null)
                {
                    sb.Append($"<image:image><image:loc>{Enc(img)}</image:loc><image:title>{Enc(post.Title)}</image:title></image:image>");
                }
                sb.AppendLine("</url>");
            }
        }

        sb.AppendLine("</urlset>");
        return sb.ToString();
    }

    public static string RenderRssFeedXml(MagSiteContext site, IReadOnlyList<MagPostCard> posts)
    {
        var cafe = string.IsNullOrWhiteSpace(site.CafeName) ? "شوگاریستا" : site.CafeName;
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<rss version=\"2.0\" xmlns:atom=\"http://www.w3.org/2005/Atom\">");
        sb.AppendLine("<channel>");
        sb.AppendLine($"  <title>مجله {Enc(cafe)}</title>");
        sb.AppendLine($"  <link>{Enc(site.BaseUrl)}/mag</link>");
        sb.AppendLine($"  <atom:link href=\"{Enc(site.BaseUrl)}/mag/feed.xml\" rel=\"self\" type=\"application/rss+xml\" />");
        sb.AppendLine($"  <description>{Enc(site.Appearance.FooterDescription)}</description>");
        sb.AppendLine("  <language>fa-ir</language>");

        foreach (var post in posts)
        {
            var link = $"{site.BaseUrl}/mag/{Uri.EscapeDataString(post.Slug)}";
            sb.AppendLine("  <item>");
            sb.AppendLine($"    <title>{Enc(post.Title)}</title>");
            sb.AppendLine($"    <link>{Enc(link)}</link>");
            sb.AppendLine($"    <guid isPermaLink=\"true\">{Enc(link)}</guid>");
            sb.AppendLine($"    <pubDate>{post.PublishedAt.ToUniversalTime():R}</pubDate>");
            if (!string.IsNullOrWhiteSpace(post.CategoryName))
            {
                sb.AppendLine($"    <category>{Enc(post.CategoryName)}</category>");
            }
            sb.AppendLine($"    <description>{Enc(post.Excerpt)}</description>");
            sb.AppendLine("  </item>");
        }

        sb.AppendLine("</channel>");
        sb.AppendLine("</rss>");
        return sb.ToString();
    }

    public static string RenderRobotsTxt(string baseUrl, bool includeBlog = true) =>
        includeBlog
            ? $"User-agent: *\nAllow: /\nAllow: /mag\nDisallow: /admin/\nDisallow: /api/\nDisallow: /db-admin/\n\nSitemap: {baseUrl}/sitemap.xml\n"
            : $"User-agent: *\nAllow: /\nDisallow: /mag\nDisallow: /admin/\nDisallow: /api/\nDisallow: /db-admin/\n\nSitemap: {baseUrl}/sitemap.xml\n";

    // ── Section Renderers ───────────────────────────────────────────────────

    private static string RenderMasthead(MagSiteContext site, string? searchQuery, string? activeCategorySlug)
    {
        var ui = site.Appearance;
        var desktopLinks = new StringBuilder();
        var mobileLinks = new StringBuilder();

        var isHomeActive = string.IsNullOrWhiteSpace(activeCategorySlug) ? " is-active" : string.Empty;
        desktopLinks.Append($"<a class=\"mag-nav-link{isHomeActive}\" href=\"/mag\">{Enc(ui.HeaderHomeLinkLabel)}</a>");
        mobileLinks.Append($"<a class=\"mag-drawer-link{isHomeActive}\" href=\"/mag\"><span>{Enc(ui.HeaderHomeLinkLabel)}</span><span>🏠</span></a>");

        foreach (var cat in site.Categories.Take(4))
        {
            var active = string.Equals(cat.Slug, activeCategorySlug, StringComparison.OrdinalIgnoreCase) ? " is-active" : string.Empty;
            desktopLinks.Append($"<a class=\"mag-nav-link{active}\" href=\"/mag/category/{Uri.EscapeDataString(cat.Slug)}\">{Enc(cat.Name)}</a>");
        }

        foreach (var cat in site.Categories)
        {
            var active = string.Equals(cat.Slug, activeCategorySlug, StringComparison.OrdinalIgnoreCase) ? " is-active" : string.Empty;
            mobileLinks.Append($"<a class=\"mag-drawer-link{active}\" href=\"/mag/category/{Uri.EscapeDataString(cat.Slug)}\"><span>{Enc(cat.Name)}</span><span class=\"mag-drawer-badge\">{ToPersianDigits(cat.PostCount)}</span></a>");
        }

        foreach (var custom in ui.CustomLinks.Where(l => l.Placement is "header" or "both"))
        {
            var isExt = custom.Url.StartsWith("http", StringComparison.OrdinalIgnoreCase);
            var extAttr = isExt ? " target=\"_blank\" rel=\"noopener noreferrer\"" : string.Empty;
            desktopLinks.Append($"<a class=\"mag-nav-link\" href=\"{Enc(custom.Url)}\"{extAttr}>{Enc(custom.Label)}</a>");
            mobileLinks.Append($"<a class=\"mag-drawer-link\" href=\"{Enc(custom.Url)}\"{extAttr}><span>{Enc(custom.Label)}</span><span>↗</span></a>");
        }

        var emblemHtml = !string.IsNullOrWhiteSpace(ui.BrandLogoImageUrl)
            ? $"<span class=\"mag-brand-emblem has-img\"><img src=\"{Enc(ui.BrandLogoImageUrl)}\" alt=\"{Enc(ui.HeaderBrandTitle)}\" /></span>"
            : $"<span class=\"mag-brand-emblem\" aria-hidden=\"true\"><span class=\"mag-ltr\">{Enc(ui.BrandMonogram)}</span></span>";

        var themeToggleHtml = ui.ShowThemeToggle
            ? "<button type=\"button\" class=\"mag-icon-btn\" id=\"mag-theme-btn\" aria-label=\"تغییر حالت روز و شب\" title=\"تغییر تم روشن / تیره\">◐</button>"
            : string.Empty;

        return $"""
            <header class="mag-header">
              <div class="mag-container mag-header-inner">
                <a class="mag-brand" href="/mag" aria-label="{Enc(ui.HeaderBrandTitle)}">
                  {emblemHtml}
                  <span class="mag-brand-copy">
                    <strong class="mag-ltr">{Enc(ui.HeaderBrandTitle)}</strong>
                    {(string.IsNullOrWhiteSpace(ui.HeaderBrandSubtitle) ? "" : $"<small>{Enc(ui.HeaderBrandSubtitle)}</small>")}
                  </span>
                </a>

                <nav class="mag-nav" aria-label="منوی مجله">
                  {desktopLinks}
                </nav>

                <div class="mag-header-actions">
                  {themeToggleHtml}
                  <a class="mag-btn mag-btn--gold mag-btn--sm mag-header-cta" href="{Enc(ui.HeaderCtaUrl)}">{Enc(ui.HeaderCtaLabel)}</a>
                  <button type="button" class="mag-icon-btn mag-mobile-toggle" id="mag-menu-toggle" aria-expanded="false" aria-controls="mag-mobile-drawer" aria-label="منوی موبایل">
                    <span class="mag-burger" aria-hidden="true"></span>
                  </button>
                </div>
              </div>

              <div class="mag-mobile-drawer" id="mag-mobile-drawer" hidden>
                <div class="mag-container mag-mobile-drawer-inner">
                  <form class="mag-drawer-search" action="/mag" method="get" role="search">
                    <input type="search" name="q" value="{Enc(searchQuery ?? "")}" placeholder="{Enc(ui.HeaderSearchPlaceholder)}" aria-label="جستجو در مقالات" />
                    <button type="submit" class="mag-btn mag-btn--gold mag-btn--sm">جستجو</button>
                  </form>
                  <nav class="mag-drawer-nav" aria-label="دسته‌بندی‌ها در موبایل">
                    {mobileLinks}
                  </nav>
                  <a class="mag-btn mag-btn--gold" href="{Enc(ui.HeaderCtaUrl)}">{Enc(ui.HeaderCtaLabel)} ☕</a>
                </div>
              </div>
            </header>
            """;
    }

    private static string RenderHomeHero(MagSiteContext site, MagPostCard? heroPost, int totalCount)
    {
        var ui = site.Appearance;
        var publishedTotal = site.TotalPublishedPosts > 0 ? site.TotalPublishedPosts : totalCount;

        var badgeHtml = !string.IsNullOrWhiteSpace(ui.HeroBadgeText)
            ? $"""
              <div class="mag-hero-pill">
                <span class="mag-hero-pill-dot" aria-hidden="true"></span>
                <span>{Enc(ui.HeroBadgeText)}</span>
              </div>
              """
            : string.Empty;

        var highlightHtml = !string.IsNullOrWhiteSpace(ui.HeroTitleHighlight)
            ? $" <span class=\"mag-hero-highlight\">{Enc(ui.HeroTitleHighlight)}</span>"
            : string.Empty;

        var suffixHtml = !string.IsNullOrWhiteSpace(ui.HeroTitleSuffix)
            ? $" {Enc(ui.HeroTitleSuffix)}"
            : string.Empty;

        var buttonsHtml = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(ui.HeroPrimaryButtonLabel))
        {
            buttonsHtml.Append($"<a class=\"mag-btn mag-btn--gold mag-btn--lg\" href=\"{Enc(ui.HeroPrimaryButtonUrl)}\">{Enc(ui.HeroPrimaryButtonLabel)}</a>");
        }
        if (!string.IsNullOrWhiteSpace(ui.HeroSecondaryButtonLabel))
        {
            buttonsHtml.Append($"<a class=\"mag-btn mag-btn--frost mag-btn--lg\" href=\"{Enc(ui.HeroSecondaryButtonUrl)}\">{Enc(ui.HeroSecondaryButtonLabel)}</a>");
        }

        var statsHtml = ui.ShowHeroStats
            ? $"""
              <div class="mag-hero-stats">
                <div class="mag-stat-chip">
                  <strong>{ToPersianDigits(publishedTotal)}</strong>
                  <span>مقاله منتشرشده</span>
                </div>
                <div class="mag-stat-chip">
                  <strong>{ToPersianDigits(site.Categories.Count)}</strong>
                  <span>موضوع تخصصی</span>
                </div>
              </div>
              """
            : string.Empty;

        string heroSpotlightCard;
        if (heroPost is not null)
        {
            var href = $"/mag/{Uri.EscapeDataString(heroPost.Slug)}";
            var media = !string.IsNullOrWhiteSpace(heroPost.CoverImageUrl)
                ? $"<img src=\"{Enc(heroPost.CoverImageUrl)}\" alt=\"{Enc(heroPost.CoverImageAlt ?? heroPost.Title)}\" fetchpriority=\"high\" decoding=\"async\" />"
                : RenderIllustratedArtwork(heroPost.CategoryName ?? "مجله شوگاریستا", heroPost.CategoryAccent, 0);

            heroSpotlightCard = $"""
                <a class="mag-hero-showcase-card" href="{href}">
                  <div class="mag-hero-showcase-media">
                    {media}
                    <span class="mag-chip mag-chip--magenta mag-hero-float-chip">✦ {Enc(ui.FeaturedBadgeLabel)}</span>
                  </div>
                  <div class="mag-hero-showcase-body">
                    <div class="mag-hero-showcase-meta">
                      {(string.IsNullOrWhiteSpace(heroPost.CategoryName) ? "" : $"<span>{Enc(heroPost.CategoryName)}</span><span>•</span>")}
                      <span>⏱ {ToPersianDigits(Math.Max(1, heroPost.ReadingTimeMinutes))} دقیقه مطالعه</span>
                    </div>
                    <strong>{Enc(heroPost.Title)}</strong>
                    <span class="mag-hero-showcase-cta">{Enc(ui.FeaturedReadButtonLabel)} ←</span>
                  </div>
                </a>
                """;
        }
        else
        {
            heroSpotlightCard = $"""
                <div class="mag-hero-showcase-card is-emblem">
                  <div class="mag-hero-showcase-media">
                    {RenderIllustratedArtwork(ui.HeaderBrandTitle, ui.AccentMagentaColor, 0)}
                  </div>
                  <div class="mag-hero-showcase-body">
                    <span class="mag-chip mag-chip--gold">☕ قهوه تخصصی و قنادی روز</span>
                    <strong>داستان طعم‌های دست‌ساز و رازهای پشت بار</strong>
                  </div>
                </div>
                """;
        }

        return $"""
            <section class="mag-hero">
              <div class="mag-hero-orb mag-hero-orb--magenta" aria-hidden="true"></div>
              <div class="mag-hero-orb mag-hero-orb--gold" aria-hidden="true"></div>
              <div class="mag-hero-orb mag-hero-orb--teal" aria-hidden="true"></div>

              <div class="mag-container mag-hero-grid">
                <div class="mag-hero-copy">
                  {badgeHtml}
                  <h1 class="mag-hero-title">{Enc(ui.HeroTitlePrefix)}{highlightHtml}{suffixHtml}</h1>
                  {(string.IsNullOrWhiteSpace(ui.HeroSubtitle) ? "" : $"<p class=\"mag-hero-subtitle\">{Enc(ui.HeroSubtitle)}</p>")}
                  {(buttonsHtml.Length > 0 ? $"<div class=\"mag-hero-actions\">{buttonsHtml}</div>" : "")}
                  {statsHtml}
                </div>

                <div class="mag-hero-visual">
                  {heroSpotlightCard}
                </div>
              </div>

              {RenderHeroWaveSvg()}
            </section>
            """;
    }

    private static string RenderFilteredHero(
        MagSiteContext site,
        string headingTitle,
        string headingSub,
        MagCategorySummary? activeCategory,
        int totalCount)
    {
        var ui = site.Appearance;
        return $"""
            <section class="mag-hero mag-hero--compact">
              <div class="mag-hero-orb mag-hero-orb--magenta" aria-hidden="true"></div>
              <div class="mag-hero-orb mag-hero-orb--gold" aria-hidden="true"></div>
              <div class="mag-container mag-filtered-hero-inner">
                <nav class="mag-breadcrumb is-on-dark" aria-label="مسیر صفحه">
                  <a href="{Enc(ui.HeaderCtaUrl)}">{Enc(ui.HeaderCtaLabel)}</a><span aria-hidden="true">/</span>
                  <a href="/mag">{Enc(ui.HeaderHomeLinkLabel)}</a><span aria-hidden="true">/</span>
                  <span class="mag-breadcrumb-current">{Enc(headingTitle)}</span>
                </nav>
                <div class="mag-filtered-title-row">
                  <div>
                    {(activeCategory is not null ? "<span class=\"mag-chip mag-chip--gold\">موضوع تخصصی</span>" : "")}
                    <h1 class="mag-filtered-h1">{Enc(headingTitle)}</h1>
                    {(string.IsNullOrWhiteSpace(headingSub) ? "" : $"<p class=\"mag-hero-subtitle\">{Enc(headingSub)}</p>")}
                  </div>
                  <span class="mag-chip mag-chip--magenta">{ToPersianDigits(totalCount)} مقاله</span>
                </div>
              </div>
              {RenderHeroWaveSvg()}
            </section>
            """;
    }

    private static string RenderHeroWaveSvg() =>
        """
        <div class="mag-hero-wave" aria-hidden="true">
          <svg viewBox="0 0 1440 68" preserveAspectRatio="none">
            <path d="M0,24 C320,76 680,-12 1080,36 C1260,56 1380,44 1440,26 L1440,68 L0,68 Z" fill="currentColor"></path>
          </svg>
        </div>
        """;

    private static string RenderCategoryDock(
        MagSiteContext site,
        MagCategorySummary? activeCategory,
        string? activeTag,
        string? searchQuery)
    {
        var ui = site.Appearance;
        var pills = new StringBuilder();
        var allActive = activeCategory is null && string.IsNullOrWhiteSpace(activeTag) ? " is-active" : string.Empty;
        pills.Append($"<a class=\"mag-cat-pill{allActive}\" href=\"/mag\">{Enc(ui.AllCategoriesChipLabel)}</a>");

        foreach (var cat in site.Categories)
        {
            var isAct = activeCategory?.Id == cat.Id ? " is-active" : string.Empty;
            var dotColor = BlogAppearanceResolver.NormalizeHex(cat.AccentColor, ui.AccentMagentaColor);
            pills.Append($"""
                <a class="mag-cat-pill{isAct}" href="/mag/category/{Uri.EscapeDataString(cat.Slug)}">
                  <span class="mag-cat-dot" style="background:{dotColor}" aria-hidden="true"></span>
                  <span>{Enc(cat.Name)}</span>
                  <span class="mag-cat-count">{ToPersianDigits(cat.PostCount)}</span>
                </a>
                """);
        }

        return $"""
            <div class="mag-container mag-dock-wrap">
              <div class="mag-filter-dock">
                <div class="mag-cat-rail" role="navigation" aria-label="دسته‌بندی‌های مجله">
                  {pills}
                </div>
                <form class="mag-dock-search" action="/mag" method="get" role="search">
                  <input type="search" name="q" value="{Enc(searchQuery ?? "")}" placeholder="{Enc(ui.HeaderSearchPlaceholder)}" aria-label="جستجو در مقالات" />
                  <button type="submit" aria-label="جستجو">🔍</button>
                </form>
              </div>
            </div>
            """;
    }

    private static string RenderHighlightsSection(BlogAppearanceSettings ui, IReadOnlyList<MagHighlightCard> highlights)
    {
        var cards = new StringBuilder();
        foreach (var item in highlights)
        {
            var theme = item.ThemeStyle is "magenta" or "gold" ? item.ThemeStyle : "teal";
            var visual = !string.IsNullOrWhiteSpace(item.ImageUrl)
                ? $"<img class=\"mag-hl-img\" src=\"{Enc(item.ImageUrl)}\" alt=\"{Enc(item.Title)}\" loading=\"lazy\" decoding=\"async\" />"
                : $"<span class=\"mag-hl-icon\" aria-hidden=\"true\">{Enc(item.IconText)}</span>";

            var ctaHtml = !string.IsNullOrWhiteSpace(item.LinkUrl)
                ? $"<span class=\"mag-hl-cta\">{Enc(string.IsNullOrWhiteSpace(item.LinkLabel) ? "مشاهده بیشتر" : item.LinkLabel)} ←</span>"
                : string.Empty;

            var inner = $"""
                <div class="mag-hl-top">
                  {visual}
                  {(string.IsNullOrWhiteSpace(item.BadgeText) ? "" : $"<span class=\"mag-hl-badge\">{Enc(item.BadgeText)}</span>")}
                </div>
                <h3>{Enc(item.Title)}</h3>
                <p>{Enc(item.Subtitle)}</p>
                {ctaHtml}
                """;

            if (!string.IsNullOrWhiteSpace(item.LinkUrl))
            {
                cards.Append($"<a class=\"mag-hl-card is-{theme}\" href=\"{Enc(item.LinkUrl)}\">{inner}</a>");
            }
            else
            {
                cards.Append($"<article class=\"mag-hl-card is-{theme}\">{inner}</article>");
            }
        }

        return $"""
            <section class="mag-highlights-section" aria-label="{Enc(ui.HighlightsSectionTitle)}">
              <div class="mag-section-head">
                <div>
                  {(string.IsNullOrWhiteSpace(ui.HighlightsSectionEyebrow) ? "" : $"<span class=\"mag-eyebrow\">{Enc(ui.HighlightsSectionEyebrow)}</span>")}
                  <h2>{Enc(ui.HighlightsSectionTitle)}</h2>
                </div>
              </div>
              <div class="mag-hl-grid">
                {cards}
              </div>
            </section>
            """;
    }

    private static string RenderFeaturedBanner(MagPostCard post, BlogAppearanceSettings ui)
    {
        var href = $"/mag/{Uri.EscapeDataString(post.Slug)}";
        var media = !string.IsNullOrWhiteSpace(post.CoverImageUrl)
            ? $"<img src=\"{Enc(post.CoverImageUrl)}\" alt=\"{Enc(post.CoverImageAlt ?? post.Title)}\" loading=\"lazy\" decoding=\"async\" />"
            : RenderIllustratedArtwork(post.CategoryName ?? "منتخب مجله", post.CategoryAccent, 1);

        return $"""
            <article class="mag-featured-banner">
              <a class="mag-featured-media" href="{href}">{media}</a>
              <div class="mag-featured-body">
                <div class="mag-featured-badges">
                  <span class="mag-chip mag-chip--magenta">✦ {Enc(ui.FeaturedBadgeLabel)}</span>
                  {(string.IsNullOrWhiteSpace(post.CategoryName) ? "" : $"<a class=\"mag-chip mag-chip--soft\" href=\"/mag/category/{Uri.EscapeDataString(post.CategorySlug ?? "")}\">{Enc(post.CategoryName)}</a>")}
                </div>
                <h2><a href="{href}">{Enc(post.Title)}</a></h2>
                <p>{Enc(post.Excerpt)}</p>
                <div class="mag-featured-footer">
                  <div class="mag-author-chip">
                    <span class="mag-author-avatar" aria-hidden="true">{Enc(FirstGrapheme(post.AuthorName))}</span>
                    <div>
                      <strong>{Enc(post.AuthorName)}</strong>
                      <small><time datetime="{post.PublishedAt:O}">{FormatShamsiDate(post.PublishedAt)}</time> • {ToPersianDigits(Math.Max(1, post.ReadingTimeMinutes))} دقیقه مطالعه</small>
                    </div>
                  </div>
                  <a class="mag-btn mag-btn--gold" href="{href}">{Enc(ui.FeaturedReadButtonLabel)} ←</a>
                </div>
              </div>
            </article>
            """;
    }

    private static string RenderPostCard(MagPostCard post, BlogAppearanceSettings ui, int index)
    {
        var href = $"/mag/{Uri.EscapeDataString(post.Slug)}";
        var accent = BlogAppearanceResolver.NormalizeHex(
            post.CategoryAccent,
            (index % 3) switch
            {
                0 => ui.PrimaryDeepColor,
                1 => ui.AccentMagentaColor,
                _ => ui.SecondaryGoldColor
            });

        var media = !string.IsNullOrWhiteSpace(post.CoverImageUrl)
            ? $"<img src=\"{Enc(post.CoverImageUrl)}\" alt=\"{Enc(post.CoverImageAlt ?? post.Title)}\" loading=\"lazy\" decoding=\"async\" />"
            : RenderIllustratedArtwork(post.CategoryName ?? "مجله طعم", accent, index);

        return $"""
            <article class="mag-card" style="--card-accent:{accent}">
              <span class="mag-card-ribbon" aria-hidden="true"></span>
              <a class="mag-card-media" href="{href}">
                {media}
                <span class="mag-card-time">⏱ {ToPersianDigits(Math.Max(1, post.ReadingTimeMinutes))} دقیقه</span>
              </a>
              <div class="mag-card-body">
                {(string.IsNullOrWhiteSpace(post.CategoryName) ? "" : $"<a class=\"mag-card-cat\" href=\"/mag/category/{Uri.EscapeDataString(post.CategorySlug ?? "")}\">{Enc(post.CategoryName)}</a>")}
                <h3><a href="{href}">{Enc(post.Title)}</a></h3>
                <p>{Enc(post.Excerpt)}</p>
                <div class="mag-card-footer">
                  <div class="mag-card-author">
                    <span class="mag-author-mini" aria-hidden="true">{Enc(FirstGrapheme(post.AuthorName))}</span>
                    <time datetime="{post.PublishedAt:O}">{FormatShamsiDate(post.PublishedAt)}</time>
                  </div>
                  <a class="mag-card-more" href="{href}">{Enc(ui.CardReadMoreLabel)} ←</a>
                </div>
              </div>
            </article>
            """;
    }

    private static string RenderIllustratedArtwork(string label, string? accent, int variant)
    {
        var safeAccent = BlogAppearanceResolver.NormalizeHex(accent, "#AA3171");
        var iconPath = (variant % 3) switch
        {
            1 => "<path d=\"M36 74 C36 52, 84 52, 84 74 Z\" stroke=\"currentColor\" stroke-width=\"2.4\"/><path d=\"M44 56 C48 44, 72 44, 76 56\" stroke=\"currentColor\" stroke-width=\"2\"/><circle cx=\"60\" cy=\"40\" r=\"4\" fill=\"currentColor\"/><path d=\"M30 78 H90\" stroke=\"currentColor\" stroke-width=\"2.6\" stroke-linecap=\"round\"/>",
            2 => "<ellipse cx=\"60\" cy=\"60\" rx=\"20\" ry=\"28\" transform=\"rotate(-20 60 60)\" stroke=\"currentColor\" stroke-width=\"2.4\"/><path d=\"M53 36 C65 54, 55 68, 67 84\" stroke=\"currentColor\" stroke-width=\"2.2\" stroke-linecap=\"round\"/>",
            _ => "<path d=\"M38 54h34v18a17 17 0 0 1-17 17h0a17 17 0 0 1-17-17V54Z\" stroke=\"currentColor\" stroke-width=\"2.4\"/><path d=\"M72 58h7a7 7 0 0 1 0 14h-7\" stroke=\"currentColor\" stroke-width=\"2.4\"/><path d=\"M48 44c0-5 5-5 5-10M60 44c0-5 5-5 5-10\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\"/><path d=\"M33 93h44\" stroke=\"currentColor\" stroke-width=\"2.4\" stroke-linecap=\"round\"/>"
        };

        return $"""
            <div class="mag-art-canvas" style="--art-tint:{safeAccent}">
              <div class="mag-art-circle c1" aria-hidden="true"></div>
              <div class="mag-art-circle c2" aria-hidden="true"></div>
              <svg viewBox="0 0 120 120" fill="none" aria-hidden="true">{iconPath}</svg>
              <span>{Enc(label)}</span>
            </div>
            """;
    }

    private static string RenderCafeCtaBanner(BlogAppearanceSettings ui)
    {
        var sideMedia = !string.IsNullOrWhiteSpace(ui.CafeCtaImageUrl)
            ? $"<div class=\"mag-cta-media\"><img src=\"{Enc(ui.CafeCtaImageUrl)}\" alt=\"{Enc(ui.CafeCtaTitle)}\" loading=\"lazy\" decoding=\"async\" /></div>"
            : string.Empty;

        return $"""
            <section class="mag-cafe-cta">
              <div class="mag-hero-orb mag-hero-orb--magenta" aria-hidden="true"></div>
              <div class="mag-hero-orb mag-hero-orb--gold" aria-hidden="true"></div>
              <div class="mag-cta-copy">
                {(string.IsNullOrWhiteSpace(ui.CafeCtaEyebrow) ? "" : $"<span class=\"mag-chip mag-chip--gold\">{Enc(ui.CafeCtaEyebrow)}</span>")}
                <h2>{Enc(ui.CafeCtaTitle)}</h2>
                {(string.IsNullOrWhiteSpace(ui.CafeCtaDescription) ? "" : $"<p>{Enc(ui.CafeCtaDescription)}</p>")}
              </div>
              {sideMedia}
              <div class="mag-cta-action">
                <a class="mag-btn mag-btn--gold mag-btn--lg" href="{Enc(ui.CafeCtaButtonUrl)}">{Enc(ui.CafeCtaButtonLabel)} ☕</a>
              </div>
            </section>
            """;
    }

    private static string RenderPagination(string basePath, string? searchQuery, int page, int totalPages)
    {
        string PageUrl(int target)
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(searchQuery))
            {
                parts.Add($"q={Uri.EscapeDataString(searchQuery)}");
            }
            if (target > 1)
            {
                parts.Add($"page={target}");
            }
            return parts.Count > 0 ? $"{basePath}?{string.Join("&", parts)}" : basePath;
        }

        var sb = new StringBuilder("<nav class=\"mag-pagination\" aria-label=\"صفحه‌بندی مقالات\">");
        if (page > 1)
        {
            sb.Append($"<a class=\"mag-btn mag-btn--outline\" href=\"{Enc(PageUrl(page - 1))}\">→ صفحه قبلی</a>");
        }
        sb.Append($"<span class=\"mag-page-info\">صفحه {ToPersianDigits(page)} از {ToPersianDigits(totalPages)}</span>");
        if (page < totalPages)
        {
            sb.Append($"<a class=\"mag-btn mag-btn--outline\" href=\"{Enc(PageUrl(page + 1))}\">صفحه بعدی ←</a>");
        }
        sb.Append("</nav>");
        return sb.ToString();
    }

    private static string RenderFooter(MagSiteContext site)
    {
        var ui = site.Appearance;
        var catLinks = new StringBuilder();
        foreach (var cat in site.Categories)
        {
            catLinks.Append($"<li><a href=\"/mag/category/{Uri.EscapeDataString(cat.Slug)}\">{Enc(cat.Name)}</a></li>");
        }
        if (site.Categories.Count == 0)
        {
            catLinks.Append($"<li><a href=\"/mag\">{Enc(ui.AllCategoriesChipLabel)}</a></li>");
        }

        var quickLinks = new StringBuilder();
        quickLinks.Append($"<li><a href=\"{Enc(ui.HeaderCtaUrl)}\">{Enc(ui.HeaderCtaLabel)}</a></li>");
        quickLinks.Append($"<li><a href=\"/mag\">{Enc(ui.HeaderHomeLinkLabel)}</a></li>");
        quickLinks.Append("<li><a href=\"/mag/feed.xml\">خوراک RSS مجله</a></li>");
        quickLinks.Append("<li><a href=\"/sitemap.xml\">نقشه سایت (Sitemap)</a></li>");
        if (!string.IsNullOrWhiteSpace(site.InstagramUrl))
        {
            quickLinks.Append($"<li><a href=\"{Enc(site.InstagramUrl)}\" target=\"_blank\" rel=\"noopener noreferrer\">اینستاگرام</a></li>");
        }
        foreach (var custom in ui.CustomLinks.Where(l => l.Placement is "footer" or "both"))
        {
            var isExt = custom.Url.StartsWith("http", StringComparison.OrdinalIgnoreCase);
            var extAttr = isExt ? " target=\"_blank\" rel=\"noopener noreferrer\"" : string.Empty;
            quickLinks.Append($"<li><a href=\"{Enc(custom.Url)}\"{extAttr}>{Enc(custom.Label)}</a></li>");
        }

        var emblemHtml = !string.IsNullOrWhiteSpace(ui.BrandLogoImageUrl)
            ? $"<span class=\"mag-brand-emblem has-img\"><img src=\"{Enc(ui.BrandLogoImageUrl)}\" alt=\"{Enc(ui.HeaderBrandTitle)}\" /></span>"
            : $"<span class=\"mag-brand-emblem\" aria-hidden=\"true\"><span class=\"mag-ltr\">{Enc(ui.BrandMonogram)}</span></span>";

        return $"""
            <footer class="mag-footer">
              <div class="mag-container mag-footer-grid">
                <div class="mag-footer-brand">
                  <div class="mag-brand">
                    {emblemHtml}
                    <span class="mag-brand-copy">
                      <strong class="mag-ltr">{Enc(ui.HeaderBrandTitle)}</strong>
                      {(string.IsNullOrWhiteSpace(ui.HeaderBrandSubtitle) ? "" : $"<small>{Enc(ui.HeaderBrandSubtitle)}</small>")}
                    </span>
                  </div>
                  {(string.IsNullOrWhiteSpace(ui.FooterDescription) ? "" : $"<p>{Enc(ui.FooterDescription)}</p>")}
                </div>
                <div>
                  <h4>{Enc(ui.FooterCategoriesHeading)}</h4>
                  <ul>{catLinks}</ul>
                </div>
                <div>
                  <h4>{Enc(ui.FooterQuickLinksHeading)}</h4>
                  <ul>{quickLinks}</ul>
                </div>
              </div>
              <div class="mag-container mag-footer-bottom">
                <span>© {ToPersianDigits(PersianCal.GetYear(DateTime.UtcNow))} — {Enc(string.IsNullOrWhiteSpace(ui.FooterCopyrightText) ? site.CafeName : ui.FooterCopyrightText)}</span>
                <div class="mag-footer-contact">
                  {(string.IsNullOrWhiteSpace(site.Address) ? "" : $"<span>📍 {Enc(site.Address)}</span>")}
                  {(string.IsNullOrWhiteSpace(site.ContactPhone) ? "" : $"<a href=\"tel:{Enc(site.ContactPhone)}\">📞 {Enc(ToPersianDigits(site.ContactPhone))}</a>")}
                </div>
              </div>
            </footer>
            """;
    }

    private static string BuildBreadcrumbJsonLd(
        MagSiteContext site,
        MagCategorySummary? category,
        string? tag,
        MagPostCard? post)
    {
        var items = new List<Dictionary<string, object?>>
        {
            new() { ["@type"] = "ListItem", ["position"] = 1, ["name"] = site.CafeName, ["item"] = $"{site.BaseUrl}/" },
            new() { ["@type"] = "ListItem", ["position"] = 2, ["name"] = site.Appearance.HeaderHomeLinkLabel, ["item"] = $"{site.BaseUrl}/mag" },
        };

        var pos = 3;
        if (category is not null)
        {
            items.Add(new()
            {
                ["@type"] = "ListItem",
                ["position"] = pos++,
                ["name"] = category.Name,
                ["item"] = $"{site.BaseUrl}/mag/category/{Uri.EscapeDataString(category.Slug)}"
            });
        }
        else if (!string.IsNullOrWhiteSpace(tag))
        {
            items.Add(new()
            {
                ["@type"] = "ListItem",
                ["position"] = pos++,
                ["name"] = tag,
                ["item"] = $"{site.BaseUrl}/mag/tag/{Uri.EscapeDataString(tag)}"
            });
        }

        if (post is not null)
        {
            items.Add(new()
            {
                ["@type"] = "ListItem",
                ["position"] = pos,
                ["name"] = post.Title,
                ["item"] = $"{site.BaseUrl}/mag/{Uri.EscapeDataString(post.Slug)}"
            });
        }

        return JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "BreadcrumbList",
            ["itemListElement"] = items
        }, JsonLdOptions);
    }

    private static string FirstGrapheme(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "ش";
        var trimmed = text.Trim();
        var enumerator = StringInfo.GetTextElementEnumerator(trimmed);
        return enumerator.MoveNext() ? enumerator.GetTextElement() : trimmed[0].ToString();
    }

    private static string? ResolveAbsoluteUrl(string baseUrl, string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return url;
        }
        return $"{baseUrl.TrimEnd('/')}/{url.TrimStart('/')}";
    }

    private static string Enc(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

    private static string RenderShell(
        MagSiteContext site,
        string title,
        string description,
        string canonicalUrl,
        string ogType,
        string? ogImage,
        bool noIndex,
        string extraHead,
        IReadOnlyList<string> jsonLdBlocks,
        string bodyHtml,
        bool includeArticleScript)
    {
        var ui = site.Appearance;
        var cafe = string.IsNullOrWhiteSpace(site.CafeName) ? "شوگاریستا" : site.CafeName;
        var absImage = ResolveAbsoluteUrl(site.BaseUrl, ogImage);
        var robots = noIndex ? "noindex, follow" : "index, follow, max-image-preview:large, max-snippet:-1";
        var defaultMode = ui.DefaultColorMode == "dark" ? "dark" : "light";

        var jsonLdHtml = new StringBuilder();
        foreach (var block in jsonLdBlocks)
        {
            var safeBlock = block.Replace("<", "\\u003c").Replace(">", "\\u003e");
            jsonLdHtml.Append("<script type=\"application/ld+json\">").Append(safeBlock).AppendLine("</script>");
        }

        var dynamicPaletteCss = $$"""
            :root{
              --mag-deep:{{ui.PrimaryDeepColor}};
              --mag-gold:{{ui.SecondaryGoldColor}};
              --mag-magenta:{{ui.AccentMagentaColor}};
              --mag-bg:{{ui.PageBackgroundColor}};
            }
            """;

        var headThemeScript = $"(function(){{try{{var s=localStorage.getItem('sugarista_mag_mode');var t=(s==='light'||s==='dark')?s:'{defaultMode}';document.documentElement.dataset.theme=t;document.documentElement.style.colorScheme=t;}}catch(e){{document.documentElement.dataset.theme='{defaultMode}';}} }})();";

        const string baseJs = """
            var b=document.getElementById('mag-theme-btn');
            if(b){b.addEventListener('click',function(){var cur=document.documentElement.dataset.theme==='dark'?'dark':'light';var nxt=cur==='dark'?'light':'dark';document.documentElement.dataset.theme=nxt;document.documentElement.style.colorScheme=nxt;try{localStorage.setItem('sugarista_mag_mode',nxt);}catch(e){} });}
            var mt=document.getElementById('mag-menu-toggle'),md=document.getElementById('mag-mobile-drawer');
            if(mt&&md){
              mt.addEventListener('click',function(){
                var open=mt.getAttribute('aria-expanded')==='true';
                mt.setAttribute('aria-expanded',open?'false':'true');
                if(open){md.setAttribute('hidden','');}else{md.removeAttribute('hidden');}
              });
              md.querySelectorAll('a').forEach(function(a){a.addEventListener('click',function(){mt.setAttribute('aria-expanded','false');md.setAttribute('hidden','');});});
            }
            """;

        const string articleJs = """
            var bar=document.getElementById('mag-progress');
            if(bar){window.addEventListener('scroll',function(){var d=document.documentElement;var h=d.scrollHeight-d.clientHeight;bar.style.width=(h>0?Math.min(100,Math.max(0,(d.scrollTop/h)*100)):0)+'%';},{passive:true});}
            document.querySelectorAll('[data-copy-url]').forEach(function(btn){btn.addEventListener('click',function(){var u=btn.getAttribute('data-copy-url');if(navigator.clipboard&&u){navigator.clipboard.writeText(u).then(function(){var t=btn.textContent;btn.textContent='کپی شد ✓';setTimeout(function(){btn.textContent=t;},1800);});}});});
            var tocLinks=document.querySelectorAll('[data-toc-link]');
            if(tocLinks.length&&'IntersectionObserver' in window){
              var map={};
              tocLinks.forEach(function(a){map[a.getAttribute('data-toc-link')]=a;});
              var ho=new IntersectionObserver(function(entries){
                entries.forEach(function(en){
                  if(en.isIntersecting){
                    tocLinks.forEach(function(x){x.classList.remove('is-active');});
                    var active=map[en.target.id];
                    if(active)active.classList.add('is-active');
                  }
                });
              },{rootMargin:'-96px 0px -68% 0px'});
              Object.keys(map).forEach(function(id){var el=document.getElementById(id);if(el)ho.observe(el);});
            }
            var mqToc=window.matchMedia?window.matchMedia('(max-width: 1024px)'):null;
            var syncToc=function(){var tb=document.querySelector('.mag-toc-box');if(!tb||!mqToc)return;if(mqToc.matches)tb.removeAttribute('open');else tb.setAttribute('open','');};
            syncToc();
            if(mqToc&&mqToc.addEventListener)mqToc.addEventListener('change',syncToc);
            """;

        var activeArticleJs = includeArticleScript ? articleJs : string.Empty;

        return $$"""
            <!DOCTYPE html>
            <html lang="fa" dir="rtl" data-theme="{{defaultMode}}">
            <head>
              <meta charset="utf-8" />
              <meta name="viewport" content="width=device-width, initial-scale=1.0" />
              <meta name="theme-color" content="{{ui.PrimaryDeepColor}}" />
              <title>{{Enc(title)}}</title>
              <meta name="description" content="{{Enc(description)}}" />
              <meta name="robots" content="{{robots}}" />
              <link rel="canonical" href="{{Enc(canonicalUrl)}}" />
              <link rel="alternate" type="application/rss+xml" title="مجله {{Enc(cafe)}}" href="{{Enc(site.BaseUrl)}}/mag/feed.xml" />
              <meta property="og:locale" content="fa_IR" />
              <meta property="og:site_name" content="مجله {{Enc(cafe)}}" />
              <meta property="og:type" content="{{ogType}}" />
              <meta property="og:title" content="{{Enc(title)}}" />
              <meta property="og:description" content="{{Enc(description)}}" />
              <meta property="og:url" content="{{Enc(canonicalUrl)}}" />
              {{(absImage is not null ? $"<meta property=\"og:image\" content=\"{Enc(absImage)}\" />" : "")}}
              <meta name="twitter:card" content="{{(absImage is not null ? "summary_large_image" : "summary")}}" />
              <meta name="twitter:title" content="{{Enc(title)}}" />
              <meta name="twitter:description" content="{{Enc(description)}}" />
              {{(absImage is not null ? $"<meta name=\"twitter:image\" content=\"{Enc(absImage)}\" />" : "")}}
              {{extraHead}}
              <link rel="preload" href="/assets/fonts/YekanBakh-Regular.woff2" as="font" type="font/woff2" crossorigin />
              <script>{{headThemeScript}}</script>
              {{jsonLdHtml}}
              <style>{{dynamicPaletteCss}}{{MagCss}}</style>
            </head>
            <body>
              <a class="mag-skip" href="#content">رفتن به محتوا</a>
              {{bodyHtml}}
              <script>
              (function(){
                {{baseJs}}
                {{activeArticleJs}}
              })();
              </script>
            </body>
            </html>
            """;
    }

    private const string MagCss = """
        @font-face{font-family:'YekanBakh';src:url('/assets/fonts/YekanBakh-Regular.woff2') format('woff2');font-weight:400 800;font-style:normal;font-display:swap;}
        :root{
          --surface:#ffffff;
          --surface-soft:#f3eee5;
          --ink:#132533;
          --muted:#586977;
          --border:rgba(12,52,79,.11);
          --border-strong:rgba(12,52,79,.22);
          --shadow-sm:0 6px 20px rgba(12,52,79,.06);
          --shadow-md:0 16px 42px rgba(12,52,79,.10);
          --shadow-lg:0 24px 58px rgba(12,52,79,.16);
          --radius-xl:26px;
          --radius-lg:20px;
          --radius-md:14px;
        }
        [data-theme="dark"]{
          --mag-bg:#071927;
          --surface:#0d2538;
          --surface-soft:#123149;
          --ink:#f2f6fa;
          --muted:#9fb5c7;
          --border:rgba(255,255,255,.11);
          --border-strong:rgba(229,170,47,.38);
          --shadow-sm:0 8px 24px rgba(0,0,0,.28);
          --shadow-md:0 18px 44px rgba(0,0,0,.38);
          --shadow-lg:0 26px 64px rgba(0,0,0,.48);
        }
        *,*::before,*::after{box-sizing:border-box;}
        html{scroll-behavior:smooth;overflow-x:clip;max-width:100%;}
        body{
          margin:0;overflow-x:clip;max-width:100%;
          font-family:'YekanBakh',system-ui,-apple-system,'Segoe UI',Tahoma,sans-serif;
          background:var(--mag-bg);color:var(--ink);line-height:1.85;-webkit-font-smoothing:antialiased;
        }
        a{color:inherit;text-decoration:none;}
        img{max-width:100%;display:block;}
        .mag-ltr{direction:ltr;unicode-bidi:embed;}
        .mag-skip{position:absolute;top:-60px;right:16px;background:var(--mag-gold);color:var(--mag-deep);font-weight:800;padding:8px 16px;border-radius:12px;z-index:300;}
        .mag-skip:focus{top:14px;}
        .mag-container{width:min(1240px,100% - clamp(28px,5vw,64px));margin-inline:auto;}
        .mag-progress-bar{position:fixed;top:0;right:0;height:4px;width:0;background:linear-gradient(90deg,var(--mag-magenta),var(--mag-gold));z-index:200;transition:width .08s linear;}

        /* ── Royal Petrol Sticky Header (#0C344F) ── */
        .mag-header{
          position:sticky;top:0;z-index:150;
          background:color-mix(in srgb,var(--mag-deep) 95%,#04131e);
          backdrop-filter:blur(16px);-webkit-backdrop-filter:blur(16px);
          border-bottom:1px solid rgba(255,255,255,.10);
          color:#ffffff;
        }
        .mag-header-inner{display:flex;align-items:center;justify-content:space-between;gap:16px;min-height:72px;padding:10px 0;}
        .mag-brand{display:inline-flex;align-items:center;gap:12px;min-width:0;flex-shrink:0;}
        .mag-brand-emblem{
          width:44px;height:44px;border-radius:13px;display:grid;place-items:center;flex-shrink:0;
          background:var(--mag-gold);color:var(--mag-deep);
          font-weight:900;font-size:1.3rem;letter-spacing:-.02em;
          box-shadow:0 8px 22px rgba(229,170,47,.34);overflow:hidden;
        }
        .mag-brand-emblem.has-img{background:#ffffff;padding:2px;}
        .mag-brand-emblem img{width:100%;height:100%;object-fit:cover;border-radius:10px;}
        .mag-brand-copy strong{display:block;font-size:1.15rem;line-height:1.15;color:#ffffff;letter-spacing:-.01em;}
        .mag-brand-copy small{display:block;font-size:.74rem;color:rgba(255,255,255,.74);margin-top:2px;}
        .mag-nav{display:flex;align-items:center;gap:6px;flex-wrap:nowrap;min-width:0;}
        .mag-nav-link{
          padding:7px 14px;border-radius:999px;font-size:.88rem;font-weight:600;white-space:nowrap;
          color:rgba(255,255,255,.82);transition:all .2s ease;
        }
        .mag-nav-link:hover{color:#ffffff;background:rgba(255,255,255,.09);}
        .mag-nav-link.is-active{background:rgba(229,170,47,.18);color:var(--mag-gold);box-shadow:inset 0 0 0 1px rgba(229,170,47,.42);}
        .mag-header-actions{display:flex;align-items:center;gap:10px;flex-shrink:0;}
        .mag-icon-btn{
          width:40px;height:40px;border-radius:999px;border:1px solid rgba(255,255,255,.18);
          background:rgba(255,255,255,.07);color:#ffffff;cursor:pointer;display:grid;place-items:center;font-size:1rem;
          transition:all .2s ease;
        }
        .mag-icon-btn:hover{border-color:var(--mag-gold);color:var(--mag-gold);}
        .mag-mobile-toggle{display:none;}
        .mag-burger{position:relative;width:17px;height:2px;background:currentColor;border-radius:2px;transition:background .2s;}
        .mag-burger::before,.mag-burger::after{content:'';position:absolute;right:0;width:17px;height:2px;background:currentColor;border-radius:2px;transition:transform .22s ease,top .22s ease;}
        .mag-burger::before{top:-5px;}
        .mag-burger::after{top:5px;}
        .mag-mobile-toggle[aria-expanded="true"] .mag-burger{background:transparent;}
        .mag-mobile-toggle[aria-expanded="true"] .mag-burger::before{top:0;transform:rotate(45deg);}
        .mag-mobile-toggle[aria-expanded="true"] .mag-burger::after{top:0;transform:rotate(-45deg);}

        .mag-mobile-drawer{border-top:1px solid rgba(255,255,255,.12);background:color-mix(in srgb,var(--mag-deep) 98%,#000);padding:16px 0 22px;}
        .mag-mobile-drawer[hidden]{display:none !important;}
        .mag-mobile-drawer-inner{display:grid;gap:12px;}
        .mag-drawer-search{display:flex;gap:8px;background:rgba(255,255,255,.08);border:1px solid rgba(255,255,255,.16);border-radius:999px;padding:5px 6px 5px 14px;}
        .mag-drawer-search input{flex:1;min-width:0;border:0;background:transparent;color:#fff;font:inherit;font-size:.9rem;outline:none;padding:6px;}
        .mag-drawer-search input::placeholder{color:rgba(255,255,255,.6);}
        .mag-drawer-nav{display:grid;gap:6px;}
        .mag-drawer-link{display:flex;align-items:center;justify-content:space-between;padding:11px 15px;border-radius:14px;background:rgba(255,255,255,.06);border:1px solid rgba(255,255,255,.1);color:#fff;font-weight:600;font-size:.92rem;}
        .mag-drawer-link.is-active{border-color:var(--mag-gold);color:var(--mag-gold);}
        .mag-drawer-badge{padding:2px 9px;border-radius:999px;background:var(--mag-magenta);color:#fff;font-size:.76rem;}

        /* ── Buttons & Chips ── */
        .mag-btn{
          display:inline-flex;align-items:center;justify-content:center;gap:8px;
          padding:11px 22px;min-height:44px;border-radius:999px;font-weight:800;font-size:.92rem;
          border:1px solid transparent;cursor:pointer;white-space:nowrap;
          transition:transform .2s ease,box-shadow .2s ease,background .2s ease,border-color .2s ease;
        }
        .mag-btn:hover{transform:translateY(-2px);}
        .mag-btn--sm{padding:7px 16px;min-height:38px;font-size:.84rem;}
        .mag-btn--lg{padding:13px 28px;min-height:50px;font-size:.96rem;}
        .mag-btn--gold{
          background:var(--mag-gold);color:var(--mag-deep);
          box-shadow:0 10px 26px color-mix(in srgb,var(--mag-gold) 38%,transparent);
        }
        .mag-btn--gold:hover{filter:brightness(1.05);box-shadow:0 14px 32px color-mix(in srgb,var(--mag-gold) 52%,transparent);}
        .mag-btn--frost{
          background:rgba(255,255,255,.10);border-color:rgba(255,255,255,.26);color:#ffffff;
          backdrop-filter:blur(10px);
        }
        .mag-btn--frost:hover{background:rgba(255,255,255,.18);border-color:var(--mag-gold);}
        .mag-btn--outline{background:var(--surface);border-color:var(--border-strong);color:var(--ink);}
        .mag-btn--outline:hover{border-color:var(--mag-magenta);color:var(--mag-magenta);}

        .mag-chip{display:inline-flex;align-items:center;gap:6px;padding:5px 13px;border-radius:999px;font-size:.78rem;font-weight:800;width:fit-content;}
        .mag-chip--magenta{background:var(--mag-magenta);color:#ffffff;box-shadow:0 6px 18px color-mix(in srgb,var(--mag-magenta) 36%,transparent);}
        .mag-chip--gold{background:var(--mag-gold);color:var(--mag-deep);}
        .mag-chip--soft{background:var(--surface-soft);color:var(--ink);border:1px solid var(--border);}

        /* ── Eye-Catching Sculpted Sugarista Hero (#0C344F + #E5AA2F + #AA3171) ── */
        .mag-hero{
          position:relative;overflow:hidden;
          background:
            radial-gradient(circle at 85% 18%,color-mix(in srgb,var(--mag-magenta) 34%,transparent),transparent 46%),
            radial-gradient(circle at 14% 78%,color-mix(in srgb,var(--mag-gold) 24%,transparent),transparent 48%),
            linear-gradient(155deg,var(--mag-deep) 0%,color-mix(in srgb,var(--mag-deep) 78%,#051624) 100%);
          color:#ffffff;
          padding:clamp(36px,5.5vw,68px) 0 clamp(72px,8vw,98px);
        }
        .mag-hero--compact{padding:32px 0 74px;}
        .mag-hero-orb{position:absolute;border-radius:50%;pointer-events:none;filter:blur(4px);}
        .mag-hero-orb--magenta{width:340px;height:340px;bottom:-110px;left:18%;background:color-mix(in srgb,var(--mag-magenta) 24%,transparent);}
        .mag-hero-orb--gold{width:280px;height:280px;top:-90px;left:-60px;background:color-mix(in srgb,var(--mag-gold) 18%,transparent);}
        .mag-hero-orb--teal{width:380px;height:380px;bottom:-150px;left:-40px;background:rgba(255,255,255,.05);}
        .mag-hero-wave{position:absolute;bottom:-1px;inset-inline:0;line-height:0;color:var(--mag-bg);pointer-events:none;}
        .mag-hero-wave svg{width:100%;height:clamp(36px,5vw,64px);display:block;}

        .mag-hero-grid{position:relative;z-index:2;display:grid;grid-template-columns:minmax(0,1.18fr) minmax(0,.88fr);gap:clamp(28px,4vw,52px);align-items:center;}
        .mag-hero-pill{
          display:inline-flex;align-items:center;gap:9px;padding:7px 16px;border-radius:999px;margin-bottom:18px;
          background:color-mix(in srgb,var(--mag-magenta) 34%,rgba(12,52,79,.6));
          border:1px solid color-mix(in srgb,var(--mag-magenta) 72%,#ffffff);
          color:#fff5fa;font-size:.83rem;font-weight:700;max-width:100%;
        }
        .mag-hero-pill-dot{width:9px;height:9px;border-radius:50%;background:var(--mag-gold);box-shadow:0 0 12px var(--mag-gold);flex-shrink:0;}
        .mag-hero-title{
          margin:0 0 18px;
          font-size:clamp(2rem,4vw,3.35rem);
          line-height:1.34;font-weight:900;letter-spacing:-.015em;color:#ffffff;
        }
        .mag-hero-highlight{
          position:relative;display:inline-block;color:var(--mag-gold);padding-bottom:4px;
        }
        .mag-hero-highlight::after{
          content:'';position:absolute;inset-inline:-2px;bottom:0;height:6px;border-radius:999px;
          background:var(--mag-magenta);box-shadow:0 3px 12px color-mix(in srgb,var(--mag-magenta) 60%,transparent);
        }
        .mag-hero-subtitle{margin:0 0 26px;font-size:clamp(.98rem,1.2vw,1.12rem);line-height:1.95;color:rgba(255,255,255,.86);max-width:58ch;}
        .mag-hero-actions{display:flex;flex-wrap:wrap;gap:12px;margin-bottom:24px;}
        .mag-hero-stats{display:flex;flex-wrap:wrap;gap:12px;}
        .mag-stat-chip{
          display:inline-flex;align-items:center;gap:8px;padding:8px 16px;border-radius:14px;
          background:rgba(255,255,255,.08);border:1px solid rgba(255,255,255,.15);backdrop-filter:blur(8px);
        }
        .mag-stat-chip strong{font-size:1.15rem;color:var(--mag-gold);}
        .mag-stat-chip span{font-size:.82rem;color:rgba(255,255,255,.82);}

        /* Hero Showcase Bento Card */
        .mag-hero-visual{min-width:0;}
        .mag-hero-showcase-card{
          display:flex;flex-direction:column;border-radius:var(--radius-xl);overflow:hidden;
          background:rgba(255,255,255,.10);backdrop-filter:blur(18px);-webkit-backdrop-filter:blur(18px);
          border:1.5px solid rgba(255,255,255,.22);box-shadow:0 24px 54px rgba(0,0,0,.32);
          transition:transform .3s ease,border-color .3s ease;
        }
        a.mag-hero-showcase-card:hover{transform:translateY(-5px);border-color:var(--mag-gold);}
        .mag-hero-showcase-media{position:relative;aspect-ratio:16/10;overflow:hidden;background:rgba(0,0,0,.2);}
        .mag-hero-showcase-media img{width:100%;height:100%;object-fit:cover;transition:transform .5s ease;}
        a.mag-hero-showcase-card:hover .mag-hero-showcase-media img{transform:scale(1.05);}
        .mag-hero-float-chip{position:absolute;top:14px;right:14px;z-index:2;}
        .mag-hero-showcase-body{padding:20px 22px;display:grid;gap:8px;color:#ffffff;}
        .mag-hero-showcase-meta{display:flex;align-items:center;gap:8px;font-size:.8rem;color:var(--mag-gold);font-weight:700;}
        .mag-hero-showcase-body strong{font-size:1.12rem;line-height:1.48;display:-webkit-box;-webkit-line-clamp:2;-webkit-box-orient:vertical;overflow:hidden;}
        .mag-hero-showcase-cta{font-size:.86rem;font-weight:800;color:var(--mag-gold);margin-top:2px;}

        .mag-filtered-hero-inner{position:relative;z-index:2;}
        .mag-filtered-title-row{display:flex;align-items:flex-end;justify-content:space-between;flex-wrap:wrap;gap:16px;}
        .mag-filtered-h1{margin:8px 0;font-size:clamp(1.75rem,3.2vw,2.6rem);line-height:1.3;}

        /* ── Floating Category & Search Dock ── */
        .mag-dock-wrap{position:relative;z-index:10;margin-top:-36px;margin-bottom:18px;}
        .mag-filter-dock{
          display:flex;align-items:center;justify-content:space-between;gap:14px;
          padding:12px 16px;border-radius:22px;
          background:var(--surface);border:1px solid var(--border);box-shadow:var(--shadow-md);
        }
        .mag-cat-rail{display:flex;align-items:center;gap:8px;overflow-x:auto;scrollbar-width:none;padding:2px 0;flex:1;min-width:0;}
        .mag-cat-rail::-webkit-scrollbar{display:none;}
        .mag-cat-pill{
          display:inline-flex;align-items:center;gap:7px;white-space:nowrap;
          padding:8px 15px;border-radius:999px;font-size:.86rem;font-weight:700;
          background:var(--surface-soft);border:1px solid var(--border);color:var(--ink);
          transition:all .2s ease;
        }
        .mag-cat-pill:hover{border-color:var(--mag-magenta);transform:translateY(-1px);}
        .mag-cat-pill.is-active{
          background:var(--mag-deep);border-color:var(--mag-deep);color:#ffffff;
          box-shadow:0 8px 20px color-mix(in srgb,var(--mag-deep) 28%,transparent);
        }
        .mag-cat-dot{width:8px;height:8px;border-radius:50%;flex-shrink:0;}
        .mag-cat-count{padding:1px 7px;border-radius:999px;font-size:.74rem;background:rgba(12,52,79,.09);}
        .mag-cat-pill.is-active .mag-cat-count{background:var(--mag-gold);color:var(--mag-deep);font-weight:900;}
        .mag-dock-search{
          display:flex;align-items:center;gap:4px;flex-shrink:0;
          background:var(--surface-soft);border:1px solid var(--border);border-radius:999px;padding:3px 12px 3px 5px;
        }
        .mag-dock-search:focus-within{border-color:var(--mag-magenta);}
        .mag-dock-search input{border:0;background:transparent;color:var(--ink);font:inherit;font-size:.85rem;width:185px;padding:5px;outline:none;}
        .mag-dock-search button{border:0;background:var(--mag-gold);color:var(--mag-deep);width:32px;height:32px;border-radius:999px;cursor:pointer;display:grid;place-items:center;font-size:.9rem;}

        .mag-content-stack{display:grid;gap:46px;padding-top:16px;padding-bottom:28px;}
        .mag-section-head{display:flex;align-items:flex-end;justify-content:space-between;flex-wrap:wrap;gap:12px;margin-bottom:20px;}
        .mag-section-head h2{margin:0;font-size:clamp(1.35rem,2.2vw,1.85rem);line-height:1.35;position:relative;}
        .mag-eyebrow{display:inline-block;font-size:.8rem;font-weight:800;color:var(--mag-magenta);margin-bottom:4px;}
        .mag-count-pill{padding:6px 14px;border-radius:999px;background:var(--surface);border:1px solid var(--border);font-size:.82rem;font-weight:800;color:var(--mag-deep);}
        [data-theme="dark"] .mag-count-pill{color:var(--mag-gold);}
        .mag-text-link{color:var(--mag-magenta);font-weight:800;font-size:.9rem;}

        /* ── Bento Highlights Strip (Teal / Magenta / Gold) ── */
        .mag-hl-grid{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:20px;}
        .mag-hl-card{
          position:relative;overflow:hidden;border-radius:var(--radius-lg);padding:24px;
          display:flex;flex-direction:column;gap:10px;box-shadow:var(--shadow-md);
          transition:transform .25s ease,box-shadow .25s ease;
        }
        .mag-hl-card:hover{transform:translateY(-5px);box-shadow:var(--shadow-lg);}
        .mag-hl-card.is-teal{
          background:linear-gradient(145deg,var(--mag-deep),color-mix(in srgb,var(--mag-deep) 74%,#061a29));
          color:#ffffff;border:1px solid rgba(255,255,255,.14);
        }
        .mag-hl-card.is-magenta{
          background:linear-gradient(145deg,var(--mag-magenta),color-mix(in srgb,var(--mag-magenta) 72%,#4d1131));
          color:#ffffff;border:1px solid rgba(255,255,255,.18);
        }
        .mag-hl-card.is-gold{
          background:linear-gradient(145deg,color-mix(in srgb,var(--mag-gold) 24%,var(--surface)),var(--surface));
          color:var(--ink);border:1.5px solid color-mix(in srgb,var(--mag-gold) 65%,transparent);
        }
        .mag-hl-top{display:flex;align-items:center;justify-content:space-between;gap:10px;margin-bottom:4px;}
        .mag-hl-icon{
          width:48px;height:48px;border-radius:14px;display:grid;place-items:center;font-size:1.45rem;
          background:rgba(255,255,255,.15);backdrop-filter:blur(6px);
        }
        .mag-hl-card.is-teal .mag-hl-icon{background:var(--mag-gold);color:var(--mag-deep);}
        .mag-hl-card.is-gold .mag-hl-icon{background:var(--mag-deep);color:var(--mag-gold);}
        .mag-hl-img{width:54px;height:54px;border-radius:14px;object-fit:cover;border:2px solid rgba(255,255,255,.3);}
        .mag-hl-badge{padding:4px 11px;border-radius:999px;font-size:.74rem;font-weight:800;background:rgba(255,255,255,.18);}
        .mag-hl-card.is-gold .mag-hl-badge{background:var(--mag-magenta);color:#ffffff;}
        .mag-hl-card h3{margin:0;font-size:1.15rem;line-height:1.4;}
        .mag-hl-card p{margin:0;font-size:.9rem;line-height:1.8;opacity:.9;}
        .mag-hl-cta{margin-top:auto;padding-top:8px;font-size:.85rem;font-weight:800;color:var(--mag-gold);}
        .mag-hl-card.is-gold .mag-hl-cta{color:var(--mag-magenta);}

        /* ── Wide Featured Story Banner ── */
        .mag-featured-banner{
          display:grid;grid-template-columns:minmax(0,1.12fr) minmax(0,1fr);
          background:var(--surface);border:1px solid var(--border);border-radius:var(--radius-xl);
          overflow:hidden;box-shadow:var(--shadow-md);
        }
        .mag-featured-media{display:block;position:relative;min-height:320px;height:100%;overflow:hidden;background:var(--surface-soft);}
        .mag-featured-media img,.mag-card-media img{width:100%;height:100%;object-fit:cover;transition:transform .5s ease;}
        .mag-featured-banner:hover .mag-featured-media img,.mag-card:hover .mag-card-media img{transform:scale(1.045);}
        .mag-featured-body{padding:clamp(24px,3.5vw,40px);display:flex;flex-direction:column;justify-content:center;gap:14px;min-width:0;}
        .mag-featured-badges{display:flex;flex-wrap:wrap;gap:8px;}
        .mag-featured-body h2{margin:0;font-size:clamp(1.38rem,2.3vw,1.95rem);line-height:1.38;}
        .mag-featured-body h2 a:hover{color:var(--mag-magenta);}
        .mag-featured-body p{margin:0;color:var(--muted);line-height:1.9;}
        .mag-featured-footer{margin-top:6px;padding-top:16px;border-top:1px solid var(--border);display:flex;align-items:center;justify-content:space-between;flex-wrap:wrap;gap:14px;}
        .mag-author-chip{display:flex;align-items:center;gap:10px;}
        .mag-author-avatar{
          width:40px;height:40px;border-radius:50%;display:grid;place-items:center;
          background:var(--mag-deep);color:var(--mag-gold);font-weight:900;font-size:.95rem;flex-shrink:0;
        }
        .mag-author-chip strong{display:block;font-size:.88rem;line-height:1.3;}
        .mag-author-chip small{display:block;font-size:.77rem;color:var(--muted);}

        /* ── Vibrant 3-Column Editorial Article Cards ── */
        .mag-grid{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:24px;}
        .mag-card{
          position:relative;display:flex;flex-direction:column;
          background:var(--surface);border:1px solid var(--border);border-radius:var(--radius-lg);
          overflow:hidden;box-shadow:var(--shadow-sm);
          transition:transform .25s ease,box-shadow .25s ease,border-color .25s ease;
        }
        .mag-card:hover{transform:translateY(-6px);box-shadow:var(--shadow-lg);border-color:var(--card-accent,var(--mag-magenta));}
        .mag-card-ribbon{height:4px;width:100%;background:linear-gradient(90deg,var(--card-accent,var(--mag-deep)),var(--mag-gold));}
        .mag-card-media{position:relative;aspect-ratio:16/10;overflow:hidden;background:var(--surface-soft);}
        .mag-card-time{
          position:absolute;bottom:10px;left:10px;padding:4px 10px;border-radius:999px;
          background:rgba(12,52,79,.86);backdrop-filter:blur(6px);color:#ffffff;font-size:.74rem;font-weight:700;
        }
        .mag-card-body{padding:20px;display:flex;flex-direction:column;gap:9px;flex:1;}
        .mag-card-cat{
          display:inline-flex;padding:3px 11px;border-radius:999px;font-size:.75rem;font-weight:800;width:fit-content;
          background:color-mix(in srgb,var(--card-accent,var(--mag-magenta)) 13%,transparent);
          color:var(--card-accent,var(--mag-magenta));
        }
        [data-theme="dark"] .mag-card-cat{background:rgba(229,170,47,.16);color:var(--mag-gold);}
        .mag-card-body h3{margin:0;font-size:1.1rem;line-height:1.48;}
        .mag-card-body h3 a:hover{color:var(--mag-magenta);}
        .mag-card-body p{margin:0;color:var(--muted);font-size:.9rem;line-height:1.8;display:-webkit-box;-webkit-line-clamp:3;-webkit-box-orient:vertical;overflow:hidden;}
        .mag-card-footer{margin-top:auto;padding-top:12px;border-top:1px solid var(--border);display:flex;align-items:center;justify-content:space-between;gap:8px;font-size:.8rem;color:var(--muted);}
        .mag-card-author{display:flex;align-items:center;gap:7px;}
        .mag-author-mini{width:24px;height:24px;border-radius:50%;display:grid;place-items:center;background:var(--mag-gold);color:var(--mag-deep);font-weight:900;font-size:.72rem;}
        .mag-card-more{font-weight:800;color:var(--mag-magenta);transition:transform .18s ease;}
        .mag-card:hover .mag-card-more{transform:translateX(-3px);}

        /* Illustrated Multi-Tone Fallback Artwork */
        .mag-art-canvas{
          position:relative;width:100%;height:100%;overflow:hidden;
          display:flex;flex-direction:column;align-items:center;justify-content:center;gap:8px;padding:18px;
          background:linear-gradient(140deg,var(--mag-deep),color-mix(in srgb,var(--art-tint) 58%,var(--mag-deep)));
          color:var(--mag-gold);
        }
        .mag-art-circle{position:absolute;border-radius:50%;pointer-events:none;}
        .mag-art-circle.c1{width:140px;height:140px;top:-40px;right:-30px;background:rgba(229,170,47,.16);}
        .mag-art-circle.c2{width:120px;height:120px;bottom:-35px;left:-25px;background:rgba(170,49,113,.28);}
        .mag-art-canvas svg{position:relative;z-index:1;width:74px;height:74px;}
        .mag-art-canvas span{position:relative;z-index:1;font-size:.82rem;font-weight:800;color:#ffffff;background:rgba(0,0,0,.24);padding:3px 12px;border-radius:999px;}

        /* ── Cafe Menu Conversion Banner ── */
        .mag-cafe-cta{
          position:relative;overflow:hidden;border-radius:var(--radius-xl);padding:clamp(26px,4vw,42px);
          background:
            radial-gradient(circle at 12% 20%,color-mix(in srgb,var(--mag-magenta) 42%,transparent),transparent 52%),
            linear-gradient(135deg,var(--mag-deep),color-mix(in srgb,var(--mag-deep) 80%,#04131e));
          color:#ffffff;box-shadow:var(--shadow-lg);
          display:flex;align-items:center;justify-content:space-between;gap:24px;flex-wrap:wrap;
        }
        .mag-cta-copy{position:relative;z-index:2;max-width:58ch;display:grid;gap:8px;}
        .mag-cta-copy h2{margin:0;font-size:clamp(1.3rem,2.2vw,1.8rem);line-height:1.38;}
        .mag-cta-copy p{margin:0;color:rgba(255,255,255,.84);}
        .mag-cta-media{position:relative;z-index:2;width:110px;height:110px;border-radius:20px;overflow:hidden;border:2px solid var(--mag-gold);flex-shrink:0;}
        .mag-cta-media img{width:100%;height:100%;object-fit:cover;}
        .mag-cta-action{position:relative;z-index:2;}

        /* Pagination & Empty */
        .mag-pagination{display:flex;align-items:center;justify-content:center;flex-wrap:wrap;gap:14px;margin-top:32px;}
        .mag-page-info{padding:8px 16px;border-radius:999px;background:var(--surface);border:1px solid var(--border);font-weight:700;font-size:.88rem;color:var(--muted);}
        .mag-empty{text-align:center;padding:54px 22px;background:var(--surface);border:1px solid var(--border);border-radius:var(--radius-xl);box-shadow:var(--shadow-sm);}
        .mag-empty-icon{width:64px;height:64px;margin:0 auto 12px;border-radius:20px;display:grid;place-items:center;background:var(--mag-deep);color:var(--mag-gold);font-size:1.8rem;}
        .mag-empty h3{margin:0 0 8px;font-size:1.3rem;}
        .mag-empty p{margin:0 auto;color:var(--muted);max-width:48ch;}
        .mag-empty-actions{display:flex;justify-content:center;flex-wrap:wrap;gap:12px;margin-top:20px;}

        /* ── Single Article Luxury Reader ── */
        .mag-article-hero{
          position:relative;overflow:hidden;
          background:
            radial-gradient(circle at 82% 20%,color-mix(in srgb,var(--mag-magenta) 34%,transparent),transparent 48%),
            linear-gradient(155deg,var(--mag-deep),color-mix(in srgb,var(--mag-deep) 78%,#04131e));
          color:#ffffff;padding:34px 0 86px;
        }
        .mag-article-hero-inner{position:relative;z-index:2;max-width:920px;}
        .mag-breadcrumb{display:flex;align-items:center;flex-wrap:wrap;gap:8px;font-size:.82rem;margin-bottom:16px;}
        .mag-breadcrumb.is-on-dark{color:rgba(255,255,255,.74);}
        .mag-breadcrumb.is-on-dark a:hover{color:var(--mag-gold);}
        .mag-breadcrumb-current{color:#ffffff;font-weight:700;max-width:44ch;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;}
        .mag-article-badges{display:flex;flex-wrap:wrap;gap:8px;margin-bottom:14px;}
        .mag-article-title{margin:0 0 14px;font-size:clamp(1.65rem,3.3vw,2.65rem);line-height:1.36;font-weight:900;}
        .mag-article-lead{margin:0 0 22px;font-size:clamp(1rem,1.2vw,1.14rem);line-height:1.95;color:rgba(255,255,255,.86);max-width:74ch;}
        .mag-meta-glass{
          display:inline-flex;flex-wrap:wrap;align-items:center;gap:10px 16px;
          padding:12px 18px;border-radius:16px;background:rgba(255,255,255,.10);
          border:1px solid rgba(255,255,255,.18);backdrop-filter:blur(10px);font-size:.85rem;
        }
        .mag-meta-pair{display:flex;align-items:center;gap:6px;}
        .mag-meta-pair span{color:rgba(255,255,255,.72);}
        .mag-meta-dot{color:var(--mag-gold);}

        .mag-article-body-wrap{position:relative;z-index:5;margin-top:-42px;padding-bottom:28px;display:grid;gap:42px;}
        .mag-article-cover{margin:0;border-radius:var(--radius-xl);overflow:hidden;background:var(--surface);border:3px solid var(--surface);box-shadow:var(--shadow-lg);}
        .mag-article-cover img{width:100%;aspect-ratio:16/8.5;object-fit:cover;}
        .mag-article-cover figcaption{padding:10px 16px;font-size:.82rem;color:var(--muted);text-align:center;background:var(--surface);}

        .mag-article-layout{display:grid;grid-template-columns:minmax(0,1fr);gap:30px;align-items:start;}
        .mag-article-layout>*{min-width:0;max-width:100%;}
        .mag-article-layout.has-toc{grid-template-columns:280px minmax(0,1fr);}
        .mag-toc-aside{position:sticky;top:90px;min-width:0;}
        .mag-toc-box{background:var(--surface);border:1px solid var(--border);border-top:4px solid var(--mag-gold);border-radius:18px;padding:16px 18px;box-shadow:var(--shadow-sm);}
        .mag-toc-box summary{font-weight:800;cursor:pointer;font-size:.94rem;list-style:none;display:flex;align-items:center;justify-content:space-between;gap:8px;color:var(--mag-deep);}
        [data-theme="dark"] .mag-toc-box summary{color:var(--mag-gold);}
        .mag-toc-box summary::-webkit-details-marker{display:none;}
        .mag-toc-list{margin:12px 0 0;padding:12px 18px 0 0;border-top:1px solid var(--border);display:grid;gap:8px;font-size:.86rem;}
        .mag-toc-list li.is-sub{margin-right:12px;font-size:.82rem;}
        .mag-toc-list a{color:var(--muted);display:block;padding:3px 6px;border-radius:8px;transition:all .18s;}
        .mag-toc-list a:hover,.mag-toc-list a.is-active{color:var(--mag-magenta);background:color-mix(in srgb,var(--mag-magenta) 10%,transparent);font-weight:800;}

        .mag-prose-card{background:var(--surface);border:1px solid var(--border);border-radius:var(--radius-xl);padding:clamp(22px,4vw,42px);box-shadow:var(--shadow-sm);}
        .mag-prose{max-width:74ch;width:100%;min-width:0;font-size:1.04rem;line-height:2.08;overflow-wrap:anywhere;}
        .mag-prose h2{margin:34px 0 14px;font-size:clamp(1.28rem,2vw,1.56rem);line-height:1.42;scroll-margin-top:96px;padding-right:14px;border-right:4px solid var(--mag-magenta);color:var(--mag-deep);}
        [data-theme="dark"] .mag-prose h2{color:var(--mag-gold);}
        .mag-prose h3{margin:26px 0 10px;font-size:1.18rem;line-height:1.45;scroll-margin-top:96px;}
        .mag-prose p{margin:0 0 18px;}
        .mag-prose ul,.mag-prose ol{margin:0 0 20px;padding-right:22px;}
        .mag-prose li{margin-bottom:8px;}
        .mag-prose li::marker{color:var(--mag-magenta);font-weight:800;}
        .mag-prose blockquote{
          margin:24px 0;padding:18px 22px;border-right:4px solid var(--mag-gold);
          background:var(--surface-soft);border-radius:16px 0 0 16px;font-weight:600;
        }
        .mag-prose blockquote p{margin:0;}
        .mag-prose code{background:var(--surface-soft);padding:2px 7px;border-radius:7px;font-size:.88em;color:var(--mag-magenta);}
        .mag-prose a{color:var(--mag-magenta);font-weight:700;text-decoration:underline;text-underline-offset:4px;}
        .mag-prose hr{border:0;border-top:1px solid var(--border);margin:30px 0;}
        .mag-figure{margin:24px 0;border-radius:18px;overflow:hidden;border:1px solid var(--border);background:var(--surface-soft);}
        .mag-figure figcaption{padding:9px 14px;font-size:.82rem;color:var(--muted);text-align:center;}
        .mag-table-wrap{max-width:100%;overflow-x:auto;-webkit-overflow-scrolling:touch;margin:24px 0;border-radius:16px;border:1px solid var(--border);}
        .mag-table-wrap table{width:100%;border-collapse:collapse;font-size:.9rem;min-width:460px;}
        .mag-table-wrap th,.mag-table-wrap td{padding:12px 15px;text-align:right;border-bottom:1px solid var(--border);}
        .mag-table-wrap thead th{background:var(--mag-deep);color:#ffffff;font-weight:800;}

        .mag-menu-showcase,.mag-faq-section{margin-top:32px;}
        .mag-menu-grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(220px,1fr));gap:14px;}
        .mag-menu-card{display:flex;align-items:center;gap:12px;padding:12px;border-radius:18px;background:var(--surface);border:1px solid var(--border);box-shadow:var(--shadow-sm);transition:all .2s;}
        .mag-menu-card:hover{border-color:var(--mag-gold);transform:translateY(-3px);box-shadow:var(--shadow-md);}
        .mag-menu-card img,.mag-menu-card-placeholder{width:60px;height:60px;border-radius:14px;object-fit:cover;flex-shrink:0;background:var(--mag-deep);color:var(--mag-gold);display:grid;place-items:center;font-size:1.4rem;}
        .mag-menu-card-body{display:grid;gap:2px;min-width:0;}
        .mag-menu-card-body strong{white-space:nowrap;overflow:hidden;text-overflow:ellipsis;}
        .mag-menu-card-body span{font-size:.74rem;color:var(--muted);}
        .mag-menu-card-body em{font-style:normal;font-size:.85rem;color:var(--mag-magenta);font-weight:800;}

        .mag-faq-list{display:grid;gap:10px;}
        .mag-faq-item{background:var(--surface);border:1px solid var(--border);border-radius:16px;padding:15px 18px;box-shadow:var(--shadow-sm);}
        .mag-faq-item[open]{border-color:var(--mag-magenta);}
        .mag-faq-item summary{font-weight:800;cursor:pointer;list-style:none;display:flex;justify-content:space-between;align-items:center;gap:12px;}
        .mag-faq-item summary::-webkit-details-marker{display:none;}
        .mag-faq-item summary::after{content:'+';width:26px;height:26px;border-radius:50%;display:grid;place-items:center;background:var(--surface-soft);color:var(--mag-magenta);font-size:1.1rem;flex-shrink:0;}
        .mag-faq-item[open] summary::after{content:'−';background:var(--mag-magenta);color:#fff;}
        .mag-faq-item p{margin:12px 0 0;padding-top:12px;border-top:1px solid var(--border);color:var(--muted);line-height:1.9;}

        .mag-article-footer{margin-top:28px;padding:20px 24px;border-radius:18px;background:var(--surface);border:1px solid var(--border);display:flex;flex-direction:column;gap:14px;}
        .mag-tags{display:flex;flex-wrap:wrap;gap:8px;}
        .mag-tag{padding:5px 13px;border-radius:999px;background:var(--surface-soft);border:1px solid var(--border);font-size:.82rem;font-weight:700;color:var(--ink);transition:all .18s;}
        .mag-tag:hover{background:var(--mag-magenta);border-color:var(--mag-magenta);color:#ffffff;}
        .mag-share-bar{display:flex;flex-wrap:wrap;align-items:center;gap:8px;font-size:.88rem;color:var(--muted);}
        .mag-share-btn{padding:7px 14px;min-height:38px;border-radius:999px;border:1px solid var(--border);background:var(--surface-soft);color:var(--ink);font:inherit;font-size:.82rem;font-weight:700;cursor:pointer;display:inline-flex;align-items:center;justify-content:center;transition:all .18s;}
        .mag-share-btn:hover{border-color:var(--mag-deep);background:var(--mag-deep);color:#ffffff;}

        /* ── Royal Petrol Editorial Footer ── */
        .mag-footer{
          margin-top:42px;
          background:color-mix(in srgb,var(--mag-deep) 94%,#031019);
          color:#ffffff;padding:48px 0 26px;
          border-top:4px solid var(--mag-gold);
        }
        .mag-footer-grid{display:grid;grid-template-columns:1.8fr 1fr 1fr;gap:32px;}
        .mag-footer-brand p{margin:14px 0 0;color:rgba(255,255,255,.78);font-size:.9rem;max-width:46ch;line-height:1.9;}
        .mag-footer h4{margin:0 0 14px;font-size:1rem;color:var(--mag-gold);}
        .mag-footer ul{margin:0;padding:0;list-style:none;display:grid;gap:8px;font-size:.9rem;color:rgba(255,255,255,.82);}
        .mag-footer a:hover{color:var(--mag-gold);}
        .mag-footer-bottom{margin-top:32px;padding-top:18px;border-top:1px solid rgba(255,255,255,.12);display:flex;justify-content:space-between;align-items:center;flex-wrap:wrap;gap:12px;font-size:.82rem;color:rgba(255,255,255,.72);}
        .mag-footer-contact{display:flex;align-items:center;flex-wrap:wrap;gap:16px;}

        /* ── Responsive Breakpoints (Desktop / Laptop / Tablet / Mobile) ── */
        @media(max-width:1080px){
          .mag-nav{display:none;}
          .mag-mobile-toggle{display:grid;}
          .mag-hl-grid{grid-template-columns:repeat(2,minmax(0,1fr));}
          .mag-grid{grid-template-columns:repeat(2,minmax(0,1fr));}
          .mag-article-layout.has-toc{grid-template-columns:minmax(0,1fr);}
          .mag-toc-aside{position:static;}
        }
        @media(max-width:860px){
          .mag-hero-grid{grid-template-columns:minmax(0,1fr);gap:28px;}
          .mag-filter-dock{flex-direction:column;align-items:stretch;}
          .mag-dock-search{width:100%;}
          .mag-dock-search input{flex:1;width:auto;}
          .mag-featured-banner{grid-template-columns:minmax(0,1fr);}
          .mag-featured-media{min-height:0;aspect-ratio:16/10;}
          .mag-footer-grid{grid-template-columns:repeat(2,minmax(0,1fr));}
          .mag-footer-brand{grid-column:1 / -1;}
        }
        @media(max-width:640px){
          .mag-header-inner{min-height:64px;gap:8px;}
          .mag-brand-copy small{display:none;}
          .mag-header-cta{padding:6px 12px;font-size:.79rem;}
          .mag-hero{padding:26px 0 68px;}
          .mag-hero-actions{display:grid;grid-template-columns:1fr;gap:10px;}
          .mag-hero-actions .mag-btn{width:100%;}
          .mag-hl-grid{grid-template-columns:minmax(0,1fr);}
          .mag-grid{grid-template-columns:minmax(0,1fr);gap:18px;}
          .mag-cafe-cta{padding:24px;}
          .mag-cta-action,.mag-cta-action .mag-btn{width:100%;}
          .mag-prose-card{padding:20px 16px;border-radius:20px;}
          .mag-meta-dot{display:none;}
          .mag-footer-grid{grid-template-columns:minmax(0,1fr);gap:24px;}
        }
        @media(max-width:380px){
          .mag-header-cta{display:none;}
        }
        @media(prefers-reduced-motion:reduce){
          html{scroll-behavior:auto;}
          *,*::before,*::after{animation-duration:.01ms !important;transition-duration:.01ms !important;}
        }
        """;
}
