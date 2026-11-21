using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Sugarista.Application.Blog;
using Sugarista.Domain.Entities;

namespace Sugarista.Api.Mag;

public sealed record MagSiteContext(
    string BaseUrl,
    string CafeName,
    string CafeDescription,
    string? ContactPhone,
    string? Address,
    string? InstagramUrl,
    IReadOnlyList<MagCategorySummary> Categories);

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
        var cafe = string.IsNullOrWhiteSpace(site.CafeName) ? "شوگریستا" : site.CafeName;
        string pageTitle;
        string metaDesc;
        string canonicalPath;
        string headingTitle;
        string headingSub;

        if (activeCategory is not null)
        {
            pageTitle = activeCategory.MetaTitle ?? $"{activeCategory.Name} | مجله قهوه {cafe}";
            metaDesc = activeCategory.MetaDescription ?? (string.IsNullOrWhiteSpace(activeCategory.Description)
                ? $"جدیدترین مقالات تخصصی {activeCategory.Name} در مجله قهوه {cafe}."
                : activeCategory.Description);
            canonicalPath = $"/mag/category/{Uri.EscapeDataString(activeCategory.Slug)}";
            headingTitle = activeCategory.Name;
            headingSub = activeCategory.Description;
        }
        else if (!string.IsNullOrWhiteSpace(activeTag))
        {
            pageTitle = $"مطالب با برچسب «{activeTag}» | مجله قهوه {cafe}";
            metaDesc = $"مجموعه مقالات تخصصی درباره {activeTag} در مجله قهوه {cafe}.";
            canonicalPath = $"/mag/tag/{Uri.EscapeDataString(activeTag)}";
            headingTitle = $"برچسب: {activeTag}";
            headingSub = $"تمام نوشته‌های تخصصی مرتبط با «{activeTag}» در مجله قهوه {cafe}.";
        }
        else if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            pageTitle = $"جستجو برای «{searchQuery}» | مجله قهوه {cafe}";
            metaDesc = $"نتایج جستجو برای {searchQuery} در مجله قهوه {cafe}.";
            canonicalPath = "/mag";
            headingTitle = $"نتایج جستجو برای «{searchQuery}»";
            headingSub = $"{ToPersianDigits(totalCount)} مقاله یافت شد.";
        }
        else
        {
            pageTitle = $"مجله قهوه {cafe} | راهنمای تخصصی قهوه، دم‌آوری و کافه";
            metaDesc = $"مجله تخصصی قهوه {cafe}؛ آموزش شناخت انواع قهوه، تفاوت عربیکا و روبوستا، روش‌های دم‌آوری اسپرسو و V60، نکات باریستا و کافه‌گردی.";
            canonicalPath = "/mag";
            headingTitle = $"مجله تخصصی قهوه {cafe}";
            headingSub = "از مزرعه و رُست تا عصاره‌گیری دقیق در فنجان؛ راهنمای کاربردی و علمی قهوه به زبان فارسی.";
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
                ["name"] = $"مجله قهوه {cafe}",
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

        // Hero intro band
        body.Append("<section class=\"mag-hero-band\"><div class=\"mag-container\">");
        body.Append("<nav class=\"mag-breadcrumb\" aria-label=\"مسیر صفحه\">");
        body.Append("<a href=\"/\">منوی کافه</a><span aria-hidden=\"true\">/</span>");
        if (activeCategory is not null || !string.IsNullOrWhiteSpace(activeTag))
        {
            body.Append("<a href=\"/mag\">مجله قهوه</a><span aria-hidden=\"true\">/</span>");
            body.Append($"<span>{Enc(headingTitle)}</span>");
        }
        else
        {
            body.Append("<span>مجله قهوه</span>");
        }
        body.Append("</nav>");
        body.Append($"<h1 class=\"mag-hero-h1\">{Enc(headingTitle)}</h1>");
        if (!string.IsNullOrWhiteSpace(headingSub))
        {
            body.Append($"<p class=\"mag-hero-sub\">{Enc(headingSub)}</p>");
        }

        // Category pills
        if (site.Categories.Count > 0)
        {
            body.Append("<div class=\"mag-cat-rail\" role=\"navigation\" aria-label=\"دسته‌بندی‌های مجله\">");
            body.Append($"<a class=\"mag-cat-pill{(activeCategory is null && string.IsNullOrWhiteSpace(activeTag) ? " is-active" : "")}\" href=\"/mag\">همه مطالب</a>");
            foreach (var cat in site.Categories)
            {
                var activeClass = activeCategory?.Id == cat.Id ? " is-active" : string.Empty;
                body.Append($"<a class=\"mag-cat-pill{activeClass}\" href=\"/mag/category/{Uri.EscapeDataString(cat.Slug)}\">{Enc(cat.Name)} <span class=\"mag-pill-count\">{ToPersianDigits(cat.PostCount)}</span></a>");
            }
            body.Append("</div>");
        }
        body.Append("</div></section>");

        body.Append("<div class=\"mag-container\">");

        // Featured editorial card (only on main /mag page 1 with no search/filter)
        if (featuredPost is not null && activeCategory is null && string.IsNullOrWhiteSpace(activeTag) && string.IsNullOrWhiteSpace(searchQuery) && page == 1)
        {
            body.Append(RenderFeaturedCard(featuredPost));
        }

        // Grid of articles
        var gridPosts = featuredPost is not null && activeCategory is null && string.IsNullOrWhiteSpace(activeTag) && string.IsNullOrWhiteSpace(searchQuery) && page == 1
            ? posts.Where(p => p.Id != featuredPost.Id).ToList()
            : posts.ToList();

        if (gridPosts.Count == 0 && (featuredPost is null || activeCategory is not null || !string.IsNullOrWhiteSpace(activeTag) || !string.IsNullOrWhiteSpace(searchQuery)))
        {
            body.Append("""
                <section class="mag-empty">
                  <h2>هنوز مقاله‌ای در این بخش منتشر نشده است</h2>
                  <p>می‌توانید سایر مقالات مجله قهوه را مرور کنید یا از منوی کافه سفارش دهید.</p>
                  <div class="mag-empty-actions">
                    <a class="mag-btn mag-btn--primary" href="/mag">مشاهده همه مطالب</a>
                    <a class="mag-btn mag-btn--ghost" href="/">رفتن به منوی کافه</a>
                  </div>
                </section>
                """);
        }
        else if (gridPosts.Count > 0)
        {
            body.Append("<section class=\"mag-grid-section\" aria-label=\"فهرست مقالات\">");
            body.Append("<div class=\"mag-grid\">");
            foreach (var post in gridPosts)
            {
                body.Append(RenderPostCard(post));
            }
            body.Append("</div>");

            if (totalPages > 1)
            {
                body.Append(RenderPagination(canonicalPath, searchQuery, page, totalPages));
            }
            body.Append("</section>");
        }

        // High-converting cafe banner
        body.Append($"""
            <section class="mag-cafe-cta">
              <div>
                <span class="mag-eyebrow">تجربه حضوری و سفارش آنلاین</span>
                <h2>هوس یک فنجان قهوهٔ تخصصی کردید؟</h2>
                <p>در منوی دیجیتال {Enc(cafe)} جدیدترین نوشیدنی‌های اسپرسو، آیس کافی، ماچا و دسرهای روز را ببینید و همین الان سفارش دهید.</p>
              </div>
              <a class="mag-btn mag-btn--primary mag-btn--lg" href="/">مشاهده منوی کافه ☕</a>
            </section>
            """);

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
        var post = vm.Post;
        var cafe = string.IsNullOrWhiteSpace(site.CafeName) ? "شوگریستا" : site.CafeName;
        var pageTitle = !string.IsNullOrWhiteSpace(vm.MetaTitle)
            ? vm.MetaTitle
            : $"{post.Title} | مجله قهوه {cafe}";
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
        body.Append("<div class=\"mag-progress-bar\" id=\"mag-progress\" aria-hidden=\"true\"></div>");
        body.Append(RenderMasthead(site, null, post.CategorySlug));

        body.Append("<main class=\"mag-main\" id=\"content\">");
        body.Append("<article class=\"mag-article-shell\" itemscope itemtype=\"https://schema.org/BlogPosting\">");
        body.Append("<div class=\"mag-container\">");

        // Breadcrumb
        body.Append("<nav class=\"mag-breadcrumb\" aria-label=\"مسیر صفحه\">");
        body.Append("<a href=\"/\">منوی کافه</a><span aria-hidden=\"true\">/</span>");
        body.Append("<a href=\"/mag\">مجله قهوه</a><span aria-hidden=\"true\">/</span>");
        if (!string.IsNullOrWhiteSpace(post.CategoryName) && !string.IsNullOrWhiteSpace(post.CategorySlug))
        {
            body.Append($"<a href=\"/mag/category/{Uri.EscapeDataString(post.CategorySlug)}\">{Enc(post.CategoryName)}</a><span aria-hidden=\"true\">/</span>");
        }
        body.Append($"<span class=\"mag-breadcrumb-current\">{Enc(post.Title)}</span>");
        body.Append("</nav>");

        // Article Header
        body.Append("<header class=\"mag-article-header\">");
        if (!string.IsNullOrWhiteSpace(post.CategoryName) && !string.IsNullOrWhiteSpace(post.CategorySlug))
        {
            body.Append($"<a class=\"mag-badge\" href=\"/mag/category/{Uri.EscapeDataString(post.CategorySlug)}\">{Enc(post.CategoryName)}</a>");
        }
        body.Append($"<h1 class=\"mag-article-title\" itemprop=\"headline\">{Enc(post.Title)}</h1>");
        if (!string.IsNullOrWhiteSpace(post.Excerpt))
        {
            body.Append($"<p class=\"mag-article-lead\" itemprop=\"description\">{Enc(post.Excerpt)}</p>");
        }
        body.Append("<div class=\"mag-meta-row\">");
        body.Append($"<span>نویسنده: <strong itemprop=\"author\">{Enc(post.AuthorName)}</strong></span>");
        body.Append($"<time datetime=\"{post.PublishedAt:O}\" itemprop=\"datePublished\">انتشار: {FormatShamsiDate(post.PublishedAt)}</time>");
        body.Append($"<span>زمان مطالعه: {ToPersianDigits(post.ReadingTimeMinutes)} دقیقه</span>");
        if (post.ViewCount > 0)
        {
            body.Append($"<span>{ToPersianDigits(post.ViewCount)} بازدید</span>");
        }
        body.Append("</div>");
        body.Append("</header>");

        // Hero cover
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

        // Layout: TOC + Prose
        var hasToc = vm.Rendered.Toc.Count >= 2;
        body.Append($"<div class=\"mag-article-layout{(hasToc ? " has-toc" : "")}\">");

        if (hasToc)
        {
            body.Append("<aside class=\"mag-toc-aside\" aria-label=\"فهرست مطالب\">");
            body.Append("<details class=\"mag-toc-box\" open>");
            body.Append("<summary>فهرست مطالب</summary>");
            body.Append("<ol class=\"mag-toc-list\">");
            foreach (var item in vm.Rendered.Toc)
            {
                var levelClass = item.Level == 3 ? " class=\"is-sub\"" : string.Empty;
                body.Append($"<li{levelClass}><a href=\"#{Enc(item.Id)}\">{Enc(item.Text)}</a></li>");
            }
            body.Append("</ol></details></aside>");
        }

        body.Append("<div class=\"mag-article-body-col\">");
        body.Append($"<div class=\"mag-prose\" itemprop=\"articleBody\">{vm.Rendered.Html}</div>");

        // Related Menu Items CTA
        if (vm.RelatedMenuItems.Count > 0)
        {
            body.Append("<section class=\"mag-menu-showcase\" aria-label=\"پیشنهاد از منوی کافه\">");
            body.Append($"<div class=\"mag-section-head\"><h2>از منوی {Enc(cafe)} امتحان کنید</h2><a href=\"/\">مشاهده همه منو ←</a></div>");
            body.Append("<div class=\"mag-menu-grid\">");
            foreach (var item in vm.RelatedMenuItems)
            {
                body.Append("<a class=\"mag-menu-card\" href=\"/\">");
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

        // FAQ Accordion (SEO FAQPage)
        if (vm.FaqItems.Count > 0)
        {
            body.Append("<section class=\"mag-faq-section\" aria-label=\"سوالات متداول\">");
            body.Append("<h2>سوالات متداول</h2>");
            body.Append("<div class=\"mag-faq-list\">");
            foreach (var faq in vm.FaqItems)
            {
                body.Append("<details class=\"mag-faq-item\">");
                body.Append($"<summary>{Enc(faq.Question)}</summary>");
                body.Append($"<p>{Enc(faq.Answer)}</p>");
                body.Append("</details>");
            }
            body.Append("</div></section>");
        }

        // Tags & Share
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

        var encodedUrl = Uri.EscapeDataString(canonicalUrl);
        var encodedTitle = Uri.EscapeDataString(post.Title);
        body.Append($"""
            <div class="mag-share-bar">
              <span>اشتراک‌گذاری:</span>
              <button type="button" class="mag-share-btn" data-copy-url="{Enc(canonicalUrl)}">کپی لینک</button>
              <a class="mag-share-btn" href="https://t.me/share/url?url={encodedUrl}&amp;text={encodedTitle}" target="_blank" rel="noopener noreferrer">تلگرام</a>
              <a class="mag-share-btn" href="https://wa.me/?text={encodedTitle}%20{encodedUrl}" target="_blank" rel="noopener noreferrer">واتساپ</a>
              <a class="mag-share-btn" href="https://x.com/intent/tweet?url={encodedUrl}&amp;text={encodedTitle}" target="_blank" rel="noopener noreferrer">X</a>
            </div>
            """);
        body.Append("</div>");

        body.Append("</div>"); // .mag-article-body-col
        body.Append("</div>"); // .mag-article-layout

        // Related Articles
        if (vm.RelatedPosts.Count > 0)
        {
            body.Append("<section class=\"mag-related-section\" aria-label=\"مطالب مرتبط\">");
            body.Append("<div class=\"mag-section-head\"><h2>مطالب مرتبط در مجله قهوه</h2><a href=\"/mag\">همه مقالات ←</a></div>");
            body.Append("<div class=\"mag-grid\">");
            foreach (var rel in vm.RelatedPosts)
            {
                body.Append(RenderPostCard(rel));
            }
            body.Append("</div></section>");
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
        var cafe = string.IsNullOrWhiteSpace(site.CafeName) ? "شوگریستا" : site.CafeName;
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<rss version=\"2.0\" xmlns:atom=\"http://www.w3.org/2005/Atom\">");
        sb.AppendLine("<channel>");
        sb.AppendLine($"  <title>مجله قهوه {Enc(cafe)}</title>");
        sb.AppendLine($"  <link>{Enc(site.BaseUrl)}/mag</link>");
        sb.AppendLine($"  <atom:link href=\"{Enc(site.BaseUrl)}/mag/feed.xml\" rel=\"self\" type=\"application/rss+xml\" />");
        sb.AppendLine($"  <description>راهنمای تخصصی قهوه، روش‌های دم‌آوری، شناخت دانه‌ها و کافه‌گردی در {Enc(cafe)}</description>");
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

    // ── Private Helpers ─────────────────────────────────────────────────────

    private static string RenderMasthead(MagSiteContext site, string? searchQuery, string? activeCategorySlug)
    {
        var cafe = string.IsNullOrWhiteSpace(site.CafeName) ? "شوگریستا" : site.CafeName;
        var navLinks = new StringBuilder();
        foreach (var cat in site.Categories.Take(4))
        {
            var active = string.Equals(cat.Slug, activeCategorySlug, StringComparison.OrdinalIgnoreCase) ? " is-active" : string.Empty;
            navLinks.Append($"<a class=\"mag-nav-link{active}\" href=\"/mag/category/{Uri.EscapeDataString(cat.Slug)}\">{Enc(cat.Name)}</a>");
        }

        return $"""
            <header class="mag-header">
              <div class="mag-container mag-header-inner">
                <a class="mag-brand" href="/mag" aria-label="مجله قهوه {Enc(cafe)}">
                  <span class="mag-brand-mark" aria-hidden="true">☕</span>
                  <span>
                    <strong>مجله {Enc(cafe)}</strong>
                    <small>SUGARISTA MAG</small>
                  </span>
                </a>
                <nav class="mag-nav" aria-label="بخش‌های مجله">
                  {navLinks}
                </nav>
                <div class="mag-header-actions">
                  <form class="mag-search-form" action="/mag" method="get" role="search">
                    <input type="search" name="q" value="{Enc(searchQuery ?? "")}" placeholder="جستجو در مقالات قهوه..." aria-label="جستجو در مقالات" />
                    <button type="submit" aria-label="جستجو">🔍</button>
                  </form>
                  <button type="button" class="mag-icon-btn" id="mag-theme-btn" aria-label="تغییر تم روشن و تیره" title="تغییر تم">◐</button>
                  <a class="mag-btn mag-btn--primary mag-btn--sm" href="/">منوی کافه</a>
                </div>
              </div>
            </header>
            """;
    }

    private static string RenderFeaturedCard(MagPostCard post)
    {
        var href = $"/mag/{Uri.EscapeDataString(post.Slug)}";
        var media = !string.IsNullOrWhiteSpace(post.CoverImageUrl)
            ? $"<img src=\"{Enc(post.CoverImageUrl)}\" alt=\"{Enc(post.CoverImageAlt ?? post.Title)}\" fetchpriority=\"high\" decoding=\"async\" />"
            : RenderFallbackArtwork(post.CategoryName ?? "قهوه تخصصی", post.CategoryAccent);

        return $"""
            <article class="mag-featured">
              <a class="mag-featured-media" href="{href}">{media}</a>
              <div class="mag-featured-body">
                <div class="mag-card-top">
                  <span class="mag-badge mag-badge--accent">منتخب تحریریه</span>
                  {(string.IsNullOrWhiteSpace(post.CategoryName) ? "" : $"<a class=\"mag-badge\" href=\"/mag/category/{Uri.EscapeDataString(post.CategorySlug ?? "")}\">{Enc(post.CategoryName)}</a>")}
                </div>
                <h2><a href="{href}">{Enc(post.Title)}</a></h2>
                <p>{Enc(post.Excerpt)}</p>
                <div class="mag-card-meta">
                  <span>{Enc(post.AuthorName)}</span>
                  <span>•</span>
                  <time datetime="{post.PublishedAt:O}">{FormatShamsiDate(post.PublishedAt)}</time>
                  <span>•</span>
                  <span>{ToPersianDigits(post.ReadingTimeMinutes)} دقیقه مطالعه</span>
                </div>
                <a class="mag-read-more" href="{href}">مطالعه کامل مقاله ←</a>
              </div>
            </article>
            """;
    }

    private static string RenderPostCard(MagPostCard post)
    {
        var href = $"/mag/{Uri.EscapeDataString(post.Slug)}";
        var media = !string.IsNullOrWhiteSpace(post.CoverImageUrl)
            ? $"<img src=\"{Enc(post.CoverImageUrl)}\" alt=\"{Enc(post.CoverImageAlt ?? post.Title)}\" loading=\"lazy\" decoding=\"async\" />"
            : RenderFallbackArtwork(post.CategoryName ?? "مجله قهوه", post.CategoryAccent);

        return $"""
            <article class="mag-card">
              <a class="mag-card-media" href="{href}">{media}</a>
              <div class="mag-card-body">
                {(string.IsNullOrWhiteSpace(post.CategoryName) ? "" : $"<a class=\"mag-badge\" href=\"/mag/category/{Uri.EscapeDataString(post.CategorySlug ?? "")}\">{Enc(post.CategoryName)}</a>")}
                <h3><a href="{href}">{Enc(post.Title)}</a></h3>
                <p>{Enc(post.Excerpt)}</p>
                <div class="mag-card-meta">
                  <time datetime="{post.PublishedAt:O}">{FormatShamsiDate(post.PublishedAt)}</time>
                  <span>•</span>
                  <span>{ToPersianDigits(post.ReadingTimeMinutes)} دقیقه</span>
                </div>
              </div>
            </article>
            """;
    }

    private static string RenderFallbackArtwork(string label, string? accent)
    {
        var color = string.IsNullOrWhiteSpace(accent) ? "#7c4a2d" : accent;
        return $"""
            <div class="mag-art-fallback" style="--art-accent:{Enc(color)}">
              <svg viewBox="0 0 120 120" fill="none" aria-hidden="true">
                <circle cx="60" cy="60" r="46" stroke="currentColor" stroke-width="1.5" opacity=".22"/>
                <path d="M38 56h34v18a16 16 0 0 1-16 16h-2a16 16 0 0 1-16-16V56Z" stroke="currentColor" stroke-width="2.4"/>
                <path d="M72 60h7a7 7 0 0 1 0 14h-7" stroke="currentColor" stroke-width="2.4"/>
                <path d="M47 45c0-5 5-5 5-10M59 45c0-5 5-5 5-10" stroke="currentColor" stroke-width="2" stroke-linecap="round"/>
                <path d="M32 94h46" stroke="currentColor" stroke-width="2.2" stroke-linecap="round"/>
              </svg>
              <span>{Enc(label)}</span>
            </div>
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
            sb.Append($"<a class=\"mag-btn mag-btn--ghost\" href=\"{Enc(PageUrl(page - 1))}\">صفحه قبلی</a>");
        }
        sb.Append($"<span class=\"mag-page-info\">صفحه {ToPersianDigits(page)} از {ToPersianDigits(totalPages)}</span>");
        if (page < totalPages)
        {
            sb.Append($"<a class=\"mag-btn mag-btn--ghost\" href=\"{Enc(PageUrl(page + 1))}\">صفحه بعدی</a>");
        }
        sb.Append("</nav>");
        return sb.ToString();
    }

    private static string RenderFooter(MagSiteContext site)
    {
        var cafe = string.IsNullOrWhiteSpace(site.CafeName) ? "شوگریستا" : site.CafeName;
        var catLinks = new StringBuilder();
        foreach (var cat in site.Categories)
        {
            catLinks.Append($"<li><a href=\"/mag/category/{Uri.EscapeDataString(cat.Slug)}\">{Enc(cat.Name)}</a></li>");
        }

        return $"""
            <footer class="mag-footer">
              <div class="mag-container mag-footer-grid">
                <div>
                  <strong>مجله قهوه {Enc(cafe)}</strong>
                  <p>{Enc(string.IsNullOrWhiteSpace(site.CafeDescription) ? "مرجع تخصصی آموزش قهوه، روش‌های دم‌آوری، شناخت دانه‌ها و تجربه کافه مدرن." : site.CafeDescription)}</p>
                </div>
                <div>
                  <h4>دسته‌بندی‌ها</h4>
                  <ul>{catLinks}</ul>
                </div>
                <div>
                  <h4>دسترسی سریع</h4>
                  <ul>
                    <li><a href="/">منوی دیجیتال و سفارش</a></li>
                    <li><a href="/mag">صفحه اصلی مجله</a></li>
                    <li><a href="/mag/feed.xml">خوراک RSS مجله</a></li>
                    <li><a href="/sitemap.xml">نقشه سایت (Sitemap)</a></li>
                  </ul>
                </div>
              </div>
              <div class="mag-container mag-footer-bottom">
                <span>© {ToPersianDigits(PersianCal.GetYear(DateTime.UtcNow))} کافه {Enc(cafe)} — تمامی حقوق محفوظ است.</span>
                {(string.IsNullOrWhiteSpace(site.ContactPhone) ? "" : $"<a href=\"tel:{Enc(site.ContactPhone)}\">تماس: {Enc(ToPersianDigits(site.ContactPhone))}</a>")}
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
            new() { ["@type"] = "ListItem", ["position"] = 2, ["name"] = "مجله قهوه", ["item"] = $"{site.BaseUrl}/mag" },
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
        var cafe = string.IsNullOrWhiteSpace(site.CafeName) ? "شوگریستا" : site.CafeName;
        var absImage = ResolveAbsoluteUrl(site.BaseUrl, ogImage);
        var robots = noIndex ? "noindex, follow" : "index, follow, max-image-preview:large, max-snippet:-1";

        var jsonLdHtml = new StringBuilder();
        foreach (var block in jsonLdBlocks)
        {
            jsonLdHtml.Append("<script type=\"application/ld+json\">").Append(block).AppendLine("</script>");
        }

        var articleJs = includeArticleScript
            ? """
              var bar=document.getElementById('mag-progress');
              if(bar){window.addEventListener('scroll',function(){var d=document.documentElement;var h=d.scrollHeight-d.clientHeight;bar.style.width=(h>0?Math.min(100,Math.max(0,(d.scrollTop/h)*100)):0)+'%';},{passive:true});}
              document.querySelectorAll('[data-copy-url]').forEach(function(btn){btn.addEventListener('click',function(){var u=btn.getAttribute('data-copy-url');if(navigator.clipboard&&u){navigator.clipboard.writeText(u).then(function(){var t=btn.textContent;btn.textContent='کپی شد ✓';setTimeout(function(){btn.textContent=t;},1800);});}});});
              """
            : string.Empty;

        return $$"""
            <!DOCTYPE html>
            <html lang="fa" dir="rtl">
            <head>
              <meta charset="utf-8" />
              <meta name="viewport" content="width=device-width, initial-scale=1.0" />
              <title>{{Enc(title)}}</title>
              <meta name="description" content="{{Enc(description)}}" />
              <meta name="robots" content="{{robots}}" />
              <link rel="canonical" href="{{Enc(canonicalUrl)}}" />
              <link rel="alternate" type="application/rss+xml" title="مجله قهوه {{Enc(cafe)}}" href="{{Enc(site.BaseUrl)}}/mag/feed.xml" />
              <meta property="og:locale" content="fa_IR" />
              <meta property="og:site_name" content="مجله قهوه {{Enc(cafe)}}" />
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
              <script>(function(){try{var p=JSON.parse(localStorage.getItem('sugarista_theme_pref')||'null');var t=(p&&p.mode==='manual'&&(p.value==='dark'||p.value==='light'))?p.value:'light';document.documentElement.dataset.theme=t;document.documentElement.style.colorScheme=t;}catch(e){} })();</script>
              {{jsonLdHtml}}
              <style>{{MagCss}}</style>
            </head>
            <body>
              <a class="mag-skip" href="#content">رفتن به محتوا</a>
              {{bodyHtml}}
              <script>
              (function(){
                var b=document.getElementById('mag-theme-btn');
                if(b){b.addEventListener('click',function(){var cur=document.documentElement.dataset.theme==='dark'?'dark':'light';var nxt=cur==='dark'?'light':'dark';document.documentElement.dataset.theme=nxt;document.documentElement.style.colorScheme=nxt;try{localStorage.setItem('sugarista_theme_pref',JSON.stringify({mode:'manual',value:nxt}));}catch(e){} });}
                {{articleJs}}
              })();
              </script>
            </body>
            </html>
            """;
    }

    private const string MagCss = """
        @font-face{font-family:'YekanBakh';src:url('/assets/fonts/YekanBakh-Regular.woff2') format('woff2');font-weight:400 800;font-style:normal;font-display:swap;}
        :root{
          --bg:#faf7f2;--surface:#ffffff;--surface-alt:#f3ece2;--ink:#231814;--muted:#6a584d;
          --border:rgba(70,46,32,.12);--accent:#7c4a2d;--brand:#aa3171;--accent-soft:rgba(124,74,45,.1);
          --shadow:0 10px 30px rgba(35,24,20,.06);--radius:18px;
        }
        [data-theme="dark"]{
          --bg:#130f0d;--surface:#1c1613;--surface-alt:#261d18;--ink:#f4ece2;--muted:#b9a799;
          --border:rgba(244,236,226,.12);--accent:#d89b56;--brand:#e06fa8;--accent-soft:rgba(216,155,86,.14);
          --shadow:0 12px 34px rgba(0,0,0,.35);
        }
        *{box-sizing:border-box;}
        html{scroll-behavior:smooth;}
        body{margin:0;font-family:'YekanBakh',system-ui,-apple-system,'Segoe UI',Tahoma,sans-serif;background:var(--bg);color:var(--ink);line-height:1.85;-webkit-font-smoothing:antialiased;}
        a{color:inherit;text-decoration:none;}
        img{max-width:100%;display:block;}
        .mag-skip{position:absolute;top:-50px;right:12px;background:var(--accent);color:#fff;padding:8px 14px;border-radius:10px;z-index:200;}
        .mag-skip:focus{top:12px;}
        .mag-container{width:min(1200px,100% - 32px);margin-inline:auto;}
        .mag-progress-bar{position:fixed;top:0;right:0;height:3px;width:0;background:linear-gradient(90deg,var(--brand),var(--accent));z-index:120;transition:width .08s linear;}
        .mag-header{position:sticky;top:0;z-index:100;background:color-mix(in srgb,var(--bg) 88%,transparent);backdrop-filter:blur(14px);border-bottom:1px solid var(--border);}
        .mag-header-inner{display:flex;align-items:center;justify-content:space-between;gap:14px;padding:12px 0;flex-wrap:wrap;}
        .mag-brand{display:inline-flex;align-items:center;gap:10px;}
        .mag-brand-mark{width:40px;height:40px;border-radius:12px;display:grid;place-items:center;background:linear-gradient(135deg,var(--accent),var(--brand));color:#fff;font-size:1.25rem;box-shadow:var(--shadow);}
        .mag-brand strong{display:block;font-size:1.05rem;line-height:1.2;}
        .mag-brand small{display:block;font-size:.66rem;letter-spacing:.14em;color:var(--muted);}
        .mag-nav{display:flex;align-items:center;gap:6px;flex-wrap:wrap;}
        .mag-nav-link{padding:6px 11px;border-radius:999px;font-size:.88rem;color:var(--muted);transition:background .15s,color .15s;}
        .mag-nav-link:hover,.mag-nav-link.is-active{background:var(--accent-soft);color:var(--accent);font-weight:700;}
        .mag-header-actions{display:flex;align-items:center;gap:8px;}
        .mag-search-form{display:flex;align-items:center;background:var(--surface);border:1px solid var(--border);border-radius:999px;padding:2px 10px 2px 4px;}
        .mag-search-form input{border:0;background:transparent;color:var(--ink);font:inherit;font-size:.85rem;width:165px;padding:6px;outline:none;}
        .mag-search-form button,.mag-icon-btn{border:0;background:transparent;color:var(--ink);cursor:pointer;width:34px;height:34px;border-radius:999px;display:grid;place-items:center;font-size:1rem;}
        .mag-icon-btn{border:1px solid var(--border);background:var(--surface);}
        .mag-btn{display:inline-flex;align-items:center;justify-content:center;gap:6px;padding:10px 18px;border-radius:999px;font-weight:700;font-size:.9rem;border:1px solid transparent;cursor:pointer;transition:transform .15s,opacity .15s;}
        .mag-btn:hover{transform:translateY(-1px);}
        .mag-btn--sm{padding:7px 14px;font-size:.84rem;}
        .mag-btn--lg{padding:13px 24px;font-size:.96rem;}
        .mag-btn--primary{background:linear-gradient(135deg,var(--accent),var(--brand));color:#fff;}
        .mag-btn--ghost{background:var(--surface);border-color:var(--border);color:var(--ink);}
        .mag-hero-band{padding:28px 0 18px;background:radial-gradient(ellipse at top left,var(--accent-soft),transparent 65%);}
        .mag-breadcrumb{display:flex;align-items:center;flex-wrap:wrap;gap:8px;font-size:.83rem;color:var(--muted);margin-bottom:12px;}
        .mag-breadcrumb a:hover{color:var(--accent);}
        .mag-breadcrumb-current{color:var(--ink);font-weight:600;max-width:46ch;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;}
        .mag-hero-h1{margin:0 0 8px;font-size:clamp(1.65rem,3vw,2.5rem);line-height:1.3;letter-spacing:-.01em;}
        .mag-hero-sub{margin:0 0 18px;color:var(--muted);max-width:68ch;font-size:1.02rem;}
        .mag-cat-rail{display:flex;gap:8px;overflow-x:auto;padding-bottom:6px;scrollbar-width:none;}
        .mag-cat-rail::-webkit-scrollbar{display:none;}
        .mag-cat-pill{display:inline-flex;align-items:center;gap:6px;white-space:nowrap;padding:7px 14px;border-radius:999px;background:var(--surface);border:1px solid var(--border);font-size:.86rem;font-weight:600;transition:all .15s;}
        .mag-cat-pill.is-active,.mag-cat-pill:hover{background:var(--accent);border-color:var(--accent);color:#fff;}
        .mag-pill-count{font-size:.76rem;opacity:.8;}
        .mag-featured{display:grid;grid-template-columns:1.15fr 1fr;gap:28px;background:var(--surface);border:1px solid var(--border);border-radius:24px;overflow:hidden;box-shadow:var(--shadow);margin:18px 0 34px;}
        .mag-featured-media{aspect-ratio:16/10;overflow:hidden;background:var(--surface-alt);}
        .mag-featured-media img,.mag-card-media img{width:100%;height:100%;object-fit:cover;transition:transform .4s ease;}
        .mag-featured:hover .mag-featured-media img,.mag-card:hover .mag-card-media img{transform:scale(1.03);}
        .mag-featured-body{padding:28px;display:flex;flex-direction:column;justify-content:center;gap:12px;}
        .mag-featured-body h2{margin:0;font-size:clamp(1.35rem,2.2vw,1.9rem);line-height:1.38;}
        .mag-featured-body p{margin:0;color:var(--muted);}
        .mag-card-top{display:flex;flex-wrap:wrap;gap:6px;}
        .mag-badge{display:inline-flex;align-items:center;padding:3px 10px;border-radius:999px;font-size:.76rem;font-weight:700;background:var(--accent-soft);color:var(--accent);width:fit-content;}
        .mag-badge--accent{background:var(--brand);color:#fff;}
        .mag-read-more{color:var(--accent);font-weight:700;margin-top:4px;width:fit-content;}
        .mag-grid{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:22px;}
        .mag-card{display:flex;flex-direction:column;background:var(--surface);border:1px solid var(--border);border-radius:var(--radius);overflow:hidden;box-shadow:var(--shadow);transition:transform .2s ease;}
        .mag-card:hover{transform:translateY(-3px);}
        .mag-card-media{aspect-ratio:16/10;overflow:hidden;background:var(--surface-alt);}
        .mag-card-body{padding:18px;display:flex;flex-direction:column;gap:8px;flex:1;}
        .mag-card-body h3{margin:0;font-size:1.08rem;line-height:1.45;}
        .mag-card-body p{margin:0;color:var(--muted);font-size:.9rem;display:-webkit-box;-webkit-line-clamp:3;-webkit-box-orient:vertical;overflow:hidden;}
        .mag-card-meta{margin-top:auto;padding-top:8px;display:flex;flex-wrap:wrap;gap:8px;font-size:.78rem;color:var(--muted);}
        .mag-art-fallback{width:100%;height:100%;display:flex;flex-direction:column;align-items:center;justify-content:center;gap:6px;background:radial-gradient(circle at 30% 25%,color-mix(in srgb,var(--art-accent) 26%,var(--surface-alt)),var(--surface-alt));color:var(--art-accent);padding:16px;}
        .mag-art-fallback svg{width:76px;height:76px;}
        .mag-art-fallback span{font-size:.82rem;font-weight:700;opacity:.85;}
        .mag-cafe-cta{margin:42px 0;padding:28px 32px;border-radius:24px;background:linear-gradient(135deg,var(--surface-alt),var(--surface));border:1px solid var(--border);display:flex;align-items:center;justify-content:space-between;gap:20px;flex-wrap:wrap;}
        .mag-eyebrow{display:inline-block;font-size:.78rem;font-weight:700;color:var(--brand);margin-bottom:4px;}
        .mag-cafe-cta h2{margin:0 0 6px;font-size:1.35rem;}
        .mag-cafe-cta p{margin:0;color:var(--muted);max-width:58ch;}
        .mag-pagination{display:flex;align-items:center;justify-content:center;gap:14px;margin:30px 0;}
        .mag-page-info{color:var(--muted);font-size:.9rem;}
        .mag-empty{text-align:center;padding:56px 20px;background:var(--surface);border:1px solid var(--border);border-radius:22px;margin:24px 0;}
        .mag-empty-actions{display:flex;justify-content:center;gap:10px;margin-top:16px;flex-wrap:wrap;}
        .mag-article-shell{padding:26px 0 48px;}
        .mag-article-header{max-width:100%;margin-bottom:22px;}
        .mag-article-title{margin:10px 0 12px;font-size:clamp(1.55rem,3vw,2.35rem);line-height:1.35;}
        .mag-article-lead{margin:0 0 14px;font-size:1.06rem;color:var(--muted);line-height:1.9;max-width:74ch;}
        .mag-meta-row{display:flex;flex-wrap:wrap;gap:14px;font-size:.86rem;color:var(--muted);padding-top:10px;border-top:1px solid var(--border);}
        .mag-article-cover{margin:0 0 28px;border-radius:22px;overflow:hidden;background:var(--surface-alt);border:1px solid var(--border);}
        .mag-article-cover img{width:100%;aspect-ratio:16/9;object-fit:cover;}
        .mag-article-cover figcaption{padding:10px 16px;font-size:.82rem;color:var(--muted);text-align:center;}
        .mag-article-layout{display:grid;grid-template-columns:minmax(0,1fr);gap:32px;align-items:start;}
        .mag-article-layout>*{min-width:0;max-width:100%;}
        .mag-article-layout.has-toc{grid-template-columns:260px minmax(0,1fr);}
        .mag-toc-aside{position:sticky;top:84px;min-width:0;}
        .mag-toc-box{background:var(--surface);border:1px solid var(--border);border-radius:16px;padding:14px 16px;max-width:100%;}
        .mag-toc-box summary{font-weight:800;cursor:pointer;font-size:.94rem;}
        .mag-toc-list{margin:10px 0 0;padding:0 18px 0 0;display:grid;gap:6px;font-size:.86rem;}
        .mag-toc-list li.is-sub{margin-right:12px;font-size:.82rem;opacity:.9;}
        .mag-toc-list a{color:var(--muted);}
        .mag-toc-list a:hover{color:var(--accent);}
        .mag-prose{max-width:72ch;width:100%;min-width:0;font-size:1.02rem;line-height:2.05;overflow-wrap:anywhere;}
        .mag-prose h2{margin:34px 0 12px;font-size:1.42rem;line-height:1.4;scroll-margin-top:90px;}
        .mag-prose h3{margin:26px 0 10px;font-size:1.18rem;line-height:1.45;scroll-margin-top:90px;}
        .mag-prose p{margin:0 0 16px;}
        .mag-prose ul,.mag-prose ol{margin:0 0 18px;padding-right:22px;}
        .mag-prose li{margin-bottom:8px;}
        .mag-prose blockquote{margin:22px 0;padding:14px 18px;border-right:4px solid var(--accent);background:var(--surface-alt);border-radius:0 12px 12px 0;}
        .mag-prose blockquote p{margin:0;}
        .mag-prose code{background:var(--surface-alt);padding:2px 6px;border-radius:6px;font-size:.9em;}
        .mag-prose a{color:var(--accent);text-decoration:underline;text-underline-offset:4px;}
        .mag-prose hr{border:0;border-top:1px solid var(--border);margin:28px 0;}
        .mag-figure{margin:24px 0;border-radius:16px;overflow:hidden;border:1px solid var(--border);background:var(--surface);}
        .mag-figure figcaption{padding:8px 14px;font-size:.82rem;color:var(--muted);text-align:center;}
        .mag-table-wrap{max-width:100%;overflow-x:auto;-webkit-overflow-scrolling:touch;margin:22px 0;border-radius:14px;border:1px solid var(--border);background:var(--surface);}
        .mag-table-wrap table{width:100%;border-collapse:collapse;font-size:.9rem;min-width:460px;}
        .mag-table-wrap th,.mag-table-wrap td{padding:11px 14px;text-align:right;border-bottom:1px solid var(--border);}
        .mag-table-wrap thead th{background:var(--surface-alt);font-weight:800;}
        .mag-menu-showcase,.mag-faq-section,.mag-related-section{margin-top:38px;}
        .mag-section-head{display:flex;align-items:center;justify-content:space-between;gap:12px;margin-bottom:16px;}
        .mag-section-head h2{margin:0;font-size:1.3rem;}
        .mag-section-head a{color:var(--accent);font-weight:700;font-size:.9rem;}
        .mag-menu-grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(200px,1fr));gap:14px;}
        .mag-menu-card{display:flex;align-items:center;gap:12px;padding:10px;border-radius:14px;background:var(--surface);border:1px solid var(--border);transition:border-color .15s;}
        .mag-menu-card:hover{border-color:var(--accent);}
        .mag-menu-card img,.mag-menu-card-placeholder{width:56px;height:56px;border-radius:12px;object-fit:cover;flex-shrink:0;background:var(--surface-alt);display:grid;place-items:center;font-size:1.4rem;}
        .mag-menu-card-body{display:grid;gap:2px;}
        .mag-menu-card-body span{font-size:.74rem;color:var(--muted);}
        .mag-menu-card-body em{font-style:normal;font-size:.84rem;color:var(--accent);font-weight:700;}
        .mag-ltr{direction:ltr;unicode-bidi:embed;}
        .mag-faq-list{display:grid;gap:10px;}
        .mag-faq-item{background:var(--surface);border:1px solid var(--border);border-radius:14px;padding:14px 18px;}
        .mag-faq-item summary{font-weight:700;cursor:pointer;list-style:none;display:flex;justify-content:space-between;align-items:center;gap:10px;}
        .mag-faq-item summary::after{content:'+';font-size:1.2rem;color:var(--accent);}
        .mag-faq-item[open] summary::after{content:'−';}
        .mag-faq-item p{margin:10px 0 0;color:var(--muted);}
        .mag-article-footer{margin-top:30px;padding-top:18px;border-top:1px solid var(--border);display:flex;flex-direction:column;gap:14px;}
        .mag-tags{display:flex;flex-wrap:wrap;gap:8px;}
        .mag-tag{padding:4px 12px;border-radius:999px;background:var(--surface-alt);font-size:.82rem;color:var(--muted);}
        .mag-tag:hover{background:var(--accent);color:#fff;}
        .mag-share-bar{display:flex;flex-wrap:wrap;align-items:center;gap:8px;font-size:.88rem;color:var(--muted);}
        .mag-share-btn{padding:6px 12px;border-radius:999px;border:1px solid var(--border);background:var(--surface);color:var(--ink);font:inherit;font-size:.82rem;cursor:pointer;}
        .mag-share-btn:hover{border-color:var(--accent);color:var(--accent);}
        .mag-footer{margin-top:48px;border-top:1px solid var(--border);background:var(--surface);padding:34px 0 22px;}
        .mag-footer-grid{display:grid;grid-template-columns:2fr 1fr 1fr;gap:28px;}
        .mag-footer h4{margin:0 0 10px;font-size:.96rem;}
        .mag-footer p{margin:8px 0 0;color:var(--muted);font-size:.9rem;max-width:44ch;}
        .mag-footer ul{margin:0;padding:0;list-style:none;display:grid;gap:6px;font-size:.88rem;color:var(--muted);}
        .mag-footer a:hover{color:var(--accent);}
        .mag-footer-bottom{margin-top:24px;padding-top:16px;border-top:1px solid var(--border);display:flex;justify-content:space-between;flex-wrap:wrap;gap:10px;font-size:.82rem;color:var(--muted);}
        @media(max-width:980px){
          .mag-featured{grid-template-columns:minmax(0,1fr);}
          .mag-grid{grid-template-columns:repeat(2,minmax(0,1fr));}
          .mag-article-layout.has-toc{grid-template-columns:minmax(0,1fr);}
          .mag-toc-aside{position:static;}
          .mag-footer-grid{grid-template-columns:repeat(2,minmax(0,1fr));}
        }
        @media(max-width:640px){
          .mag-nav{display:none;}
          .mag-search-form input{width:120px;}
          .mag-grid{grid-template-columns:minmax(0,1fr);}
          .mag-featured-body{padding:20px;}
          .mag-cafe-cta{padding:22px;}
          .mag-footer-grid{grid-template-columns:minmax(0,1fr);}
        }
        """;
}
