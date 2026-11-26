using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Sugarista.Api.Mag;
using Sugarista.Application.Blog;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Domain.Entities;

namespace Sugarista.Api.Controllers;

[AllowAnonymous]
public class MagController : ControllerBase
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly IMemoryCache _memoryCache;
    private readonly IMagCacheService _magCache;

    public MagController(
        IApplicationDbContext dbContext,
        IConfiguration configuration,
        IMemoryCache memoryCache,
        IMagCacheService magCache)
    {
        _dbContext = dbContext;
        _configuration = configuration;
        _memoryCache = memoryCache;
        _magCache = magCache;
    }

    [HttpGet("/mag")]
    [HttpHead("/mag")]
    public Task<IActionResult> Index(
        [FromQuery] string? q = null,
        [FromQuery] int page = 1,
        CancellationToken cancellationToken = default) =>
        RenderArchiveAsync(categorySlug: null, tag: null, searchQuery: q, page: page, cancellationToken);

    [HttpGet("/mag/category/{slug}")]
    [HttpHead("/mag/category/{slug}")]
    public Task<IActionResult> Category(
        string slug,
        [FromQuery] int page = 1,
        CancellationToken cancellationToken = default) =>
        RenderArchiveAsync(categorySlug: slug, tag: null, searchQuery: null, page: page, cancellationToken);

    [HttpGet("/mag/tag/{tag}")]
    [HttpHead("/mag/tag/{tag}")]
    public Task<IActionResult> Tag(
        string tag,
        [FromQuery] int page = 1,
        CancellationToken cancellationToken = default) =>
        RenderArchiveAsync(categorySlug: null, tag: tag, searchQuery: null, page: page, cancellationToken);

    [HttpGet("/mag/{slug}")]
    [HttpHead("/mag/{slug}")]
    public async Task<IActionResult> Article(string slug, CancellationToken cancellationToken = default)
    {
        if (!await IsBlogEnabledAsync(cancellationToken))
        {
            return BlogDisabledNotFound();
        }

        var normalizedSlug = BlogCategory.NormalizeSlug(Uri.UnescapeDataString(slug));
        var now = DateTimeOffset.UtcNow;

        var post = await _dbContext.BlogPosts
            .AsNoTracking()
            .Include(p => p.Category)
            .FirstOrDefaultAsync(
                p => p.Slug == normalizedSlug &&
                     (p.Status == BlogPostStatus.Published ||
                      (p.Status == BlogPostStatus.Scheduled && p.PublishedAt <= now)),
                cancellationToken);

        if (post is null)
        {
            return NotFound("مقاله مورد نظر یافت نشد.");
        }

        long incrementedViewCount = post.ViewCount;
        if (!HttpMethods.IsHead(Request.Method))
        {
            try
            {
                await _dbContext.BlogPosts
                    .Where(p => p.Id == post.Id)
                    .ExecuteUpdateAsync(
                        setters => setters.SetProperty(p => p.ViewCount, p => p.ViewCount + 1),
                        cancellationToken);
                incrementedViewCount = post.ViewCount + 1;
            }
            catch
            {
                // View counter must never break rendering
            }
        }

        var site = await LoadSiteContextAsync(now, cancellationToken);
        var rendered = BlogMarkdownRenderer.Render(post.ContentMarkdown);
        var faqItems = BlogMarkdownRenderer.ParseFaqItems(post.FaqJson);

        var relatedPosts = await _dbContext.BlogPosts
            .AsNoTracking()
            .Include(p => p.Category)
            .Where(p => p.Id != post.Id &&
                        (p.Status == BlogPostStatus.Published ||
                         (p.Status == BlogPostStatus.Scheduled && p.PublishedAt <= now)))
            .OrderByDescending(p => p.BlogCategoryId == post.BlogCategoryId)
            .ThenByDescending(p => p.PublishedAt ?? p.CreatedAt)
            .Take(3)
            .Select(p => ToCard(p))
            .ToListAsync(cancellationToken);

        List<MagMenuItemCard> relatedMenuItems = [];
        if (post.RelatedMenuItemIds.Count > 0)
        {
            relatedMenuItems = await _dbContext.MenuItems
                .AsNoTracking()
                .Include(m => m.Category)
                .Where(m => post.RelatedMenuItemIds.Contains(m.Id) && m.IsActive)
                .Select(m => new MagMenuItemCard(
                    m.Id,
                    m.PersianName,
                    m.EnglishName,
                    m.ImageUrl,
                    m.SpecialOfferPrice ?? m.Price,
                    m.Category.Name))
                .ToListAsync(cancellationToken);
        }

        var postCard = ToCard(post) with { ViewCount = incrementedViewCount };
        var html = MagHtmlRenderer.RenderArticlePage(
            site,
            new MagArticleViewModel(
                postCard,
                rendered,
                faqItems,
                relatedPosts,
                relatedMenuItems,
                post.MetaTitle,
                post.MetaDescription,
                post.FocusKeyword,
                post.CanonicalUrl,
                post.NoIndex));

        return ETagContent(html, "text/html; charset=utf-8");
    }

    [HttpGet("/mag/feed.xml")]
    [HttpHead("/mag/feed.xml")]
    public async Task<IActionResult> RssFeed(CancellationToken cancellationToken = default)
    {
        if (!await IsBlogEnabledAsync(cancellationToken))
        {
            return BlogDisabledNotFound();
        }

        var cacheKey = $"mag:v{_magCache.Version}:rss:{ResolvePublicBaseUrl()}";
        if (!_memoryCache.TryGetValue(cacheKey, out string? xml) || xml is null)
        {
            var now = DateTimeOffset.UtcNow;
            var site = await LoadSiteContextAsync(now, cancellationToken);
            var posts = await _dbContext.BlogPosts
                .AsNoTracking()
                .Include(p => p.Category)
                .Where(p => !p.NoIndex &&
                            (p.Status == BlogPostStatus.Published ||
                             (p.Status == BlogPostStatus.Scheduled && p.PublishedAt <= now)))
                .OrderByDescending(p => p.PublishedAt ?? p.CreatedAt)
                .Take(25)
                .Select(p => ToCard(p))
                .ToListAsync(cancellationToken);

            xml = MagHtmlRenderer.RenderRssFeedXml(site, posts);
            _memoryCache.Set(cacheKey, xml, TimeSpan.FromMinutes(5));
        }

        return ETagContent(xml, "application/rss+xml; charset=utf-8");
    }

    [HttpGet("/sitemap.xml")]
    [HttpHead("/sitemap.xml")]
    public async Task<IActionResult> Sitemap(CancellationToken cancellationToken = default)
    {
        var baseUrl = ResolvePublicBaseUrl();
        var cacheKey = $"mag:v{_magCache.Version}:sitemap:{baseUrl}";

        if (!_memoryCache.TryGetValue(cacheKey, out string? xml) || xml is null)
        {
            var now = DateTimeOffset.UtcNow;
            var blogEnabled = await IsBlogEnabledAsync(cancellationToken);

            if (!blogEnabled)
            {
                xml = MagHtmlRenderer.RenderSitemapXml(baseUrl, [], [], includeBlog: false);
            }
            else
            {
                var categories = await _dbContext.BlogCategories
                    .AsNoTracking()
                    .Where(c => c.IsActive)
                    .OrderBy(c => c.DisplayOrder)
                    .Select(c => new { c.Slug, LastMod = c.LastModifiedAt ?? c.CreatedAt })
                    .ToListAsync(cancellationToken);

                var posts = await _dbContext.BlogPosts
                    .AsNoTracking()
                    .Where(p => !p.NoIndex &&
                                (p.Status == BlogPostStatus.Published ||
                                 (p.Status == BlogPostStatus.Scheduled && p.PublishedAt <= now)))
                    .OrderByDescending(p => p.PublishedAt ?? p.CreatedAt)
                    .Select(p => new
                    {
                        p.Slug,
                        LastMod = p.LastModifiedAt ?? p.PublishedAt ?? p.CreatedAt,
                        p.CoverImageUrl,
                        p.Title
                    })
                    .ToListAsync(cancellationToken);

                xml = MagHtmlRenderer.RenderSitemapXml(
                    baseUrl,
                    categories.Select(c => (c.Slug, c.LastMod)).ToList(),
                    posts.Select(p => (p.Slug, p.LastMod, p.CoverImageUrl, p.Title)).ToList(),
                    includeBlog: true);
            }

            _memoryCache.Set(cacheKey, xml, TimeSpan.FromMinutes(5));
        }

        return ETagContent(xml, "application/xml; charset=utf-8");
    }

    [HttpGet("/robots.txt")]
    [HttpHead("/robots.txt")]
    public async Task<IActionResult> Robots(CancellationToken cancellationToken = default)
    {
        var blogEnabled = await IsBlogEnabledAsync(cancellationToken);
        var txt = MagHtmlRenderer.RenderRobotsTxt(ResolvePublicBaseUrl(), includeBlog: blogEnabled);
        return ETagContent(txt, "text/plain; charset=utf-8");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private async Task<bool> IsBlogEnabledAsync(CancellationToken cancellationToken)
    {
        var cacheKey = $"mag:v{_magCache.Version}:enabled";
        if (_memoryCache.TryGetValue(cacheKey, out bool cached))
        {
            return cached;
        }

        var enabled = await _dbContext.SiteSettings
            .AsNoTracking()
            .Select(s => (bool?)s.IsBlogEnabled)
            .FirstOrDefaultAsync(cancellationToken) ?? true;

        _memoryCache.Set(cacheKey, enabled, TimeSpan.FromMinutes(5));
        return enabled;
    }

    private IActionResult BlogDisabledNotFound()
    {
        Response.Headers.CacheControl = "no-store";
        return NotFound();
    }

    private async Task<IActionResult> RenderArchiveAsync(
        string? categorySlug,
        string? tag,
        string? searchQuery,
        int page,
        CancellationToken cancellationToken)
    {
        if (!await IsBlogEnabledAsync(cancellationToken))
        {
            return BlogDisabledNotFound();
        }

        var safePage = Math.Max(1, page);
        var cleanTag = string.IsNullOrWhiteSpace(tag) ? null : Uri.UnescapeDataString(tag).Trim();
        var cleanSearch = string.IsNullOrWhiteSpace(searchQuery) ? null : searchQuery.Trim();
        var baseUrl = ResolvePublicBaseUrl();

        var cacheKey = cleanSearch is null
            ? $"mag:v{_magCache.Version}:archive:{baseUrl}:cat={categorySlug ?? "-"}:tag={cleanTag ?? "-"}:p={safePage}"
            : null;

        if (cacheKey is not null && _memoryCache.TryGetValue(cacheKey, out string? cachedHtml) && cachedHtml is not null)
        {
            return ETagContent(cachedHtml, "text/html; charset=utf-8");
        }

        var now = DateTimeOffset.UtcNow;
        var site = await LoadSiteContextAsync(now, cancellationToken);
        var pageSize = Math.Clamp(site.Appearance.PostsPerPage, 3, 24);

        MagCategorySummary? activeCategory = null;
        if (!string.IsNullOrWhiteSpace(categorySlug))
        {
            var normalizedCat = BlogCategory.NormalizeSlug(Uri.UnescapeDataString(categorySlug));
            activeCategory = site.Categories.FirstOrDefault(c =>
                string.Equals(c.Slug, normalizedCat, StringComparison.OrdinalIgnoreCase));
            if (activeCategory is null)
            {
                return NotFound("دسته‌بندی مورد نظر یافت نشد.");
            }
        }

        var query = _dbContext.BlogPosts
            .AsNoTracking()
            .Include(p => p.Category)
            .Where(p => p.Status == BlogPostStatus.Published ||
                        (p.Status == BlogPostStatus.Scheduled && p.PublishedAt <= now));

        if (activeCategory is not null)
        {
            query = query.Where(p => p.BlogCategoryId == activeCategory.Id);
        }

        if (cleanTag is not null)
        {
            query = query.Where(p => p.Tags.Contains(cleanTag));
        }

        if (cleanSearch is not null)
        {
            var term = cleanSearch.ToLowerInvariant();
            query = query.Where(p =>
                p.Title.ToLower().Contains(term) ||
                p.Excerpt.ToLower().Contains(term) ||
                p.ContentMarkdown.ToLower().Contains(term) ||
                (p.FocusKeyword != null && p.FocusKeyword.ToLower().Contains(term)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        if (safePage > totalPages) safePage = totalPages;

        var pagePosts = await query
            .OrderByDescending(p => p.IsFeatured)
            .ThenBy(p => p.DisplayOrder)
            .ThenByDescending(p => p.PublishedAt ?? p.CreatedAt)
            .Skip((safePage - 1) * pageSize)
            .Take(pageSize)
            .Select(p => ToCard(p))
            .ToListAsync(cancellationToken);

        var featured = activeCategory is null && cleanTag is null && cleanSearch is null && safePage == 1
            ? pagePosts.FirstOrDefault(p => p.IsFeatured) ?? pagePosts.FirstOrDefault()
            : null;

        var html = MagHtmlRenderer.RenderIndexPage(
            site,
            pagePosts,
            featured,
            cleanSearch,
            activeCategory,
            cleanTag,
            safePage,
            totalPages,
            totalCount);

        if (cacheKey is not null)
        {
            _memoryCache.Set(cacheKey, html, new MemoryCacheEntryOptions
            {
                SlidingExpiration = TimeSpan.FromMinutes(3),
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
            });
        }

        return ETagContent(html, "text/html; charset=utf-8", noStore: cleanSearch is not null);
    }

    private async Task<MagSiteContext> LoadSiteContextAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var baseUrl = ResolvePublicBaseUrl();
        var cacheKey = $"mag:v{_magCache.Version}:site:{baseUrl}";

        if (_memoryCache.TryGetValue(cacheKey, out MagSiteContext? cachedSite) && cachedSite is not null)
        {
            return cachedSite;
        }

        var settings = await _dbContext.SiteSettings
            .AsNoTracking()
            .Select(s => new
            {
                s.CafeName,
                s.MetaDescription,
                s.ContactPhone,
                s.Address,
                s.InstagramUrl,
                s.BlogSettingsJson
            })
            .FirstOrDefaultAsync(cancellationToken);

        var categories = await _dbContext.BlogCategories
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.Name)
            .Select(c => new MagCategorySummary(
                c.Id,
                c.Name,
                c.Slug,
                c.Description,
                c.MetaTitle,
                c.MetaDescription,
                c.AccentColor,
                c.Posts.Count(p =>
                    p.Status == BlogPostStatus.Published ||
                    (p.Status == BlogPostStatus.Scheduled && p.PublishedAt <= now))))
            .ToListAsync(cancellationToken);

        var highlights = await _dbContext.BlogHighlights
            .AsNoTracking()
            .Where(h => h.IsActive)
            .OrderBy(h => h.DisplayOrder)
            .ThenBy(h => h.CreatedAt)
            .Take(12)
            .Select(h => new MagHighlightCard(
                h.Id,
                h.Title,
                h.Subtitle,
                h.BadgeText,
                h.IconText,
                h.ImageUrl,
                h.LinkUrl,
                h.LinkLabel,
                h.ThemeStyle))
            .ToListAsync(cancellationToken);

        var totalPublished = await _dbContext.BlogPosts
            .AsNoTracking()
            .CountAsync(
                p => p.Status == BlogPostStatus.Published ||
                     (p.Status == BlogPostStatus.Scheduled && p.PublishedAt <= now),
                cancellationToken);

        var cafeName = settings?.CafeName ?? "شوگاریستا";
        var appearance = BlogAppearanceResolver.Resolve(settings?.BlogSettingsJson, cafeName);

        var context = new MagSiteContext(
            baseUrl,
            cafeName,
            settings?.MetaDescription ?? string.Empty,
            settings?.ContactPhone,
            settings?.Address,
            settings?.InstagramUrl,
            categories,
            appearance,
            highlights,
            totalPublished);

        _memoryCache.Set(cacheKey, context, new MemoryCacheEntryOptions
        {
            SlidingExpiration = TimeSpan.FromMinutes(5),
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15)
        });

        return context;
    }

    private string ResolvePublicBaseUrl()
    {
        var configured = _configuration["App:PublicOrigin"]?.Trim();
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured.TrimEnd('/');
        }

        var scheme = Request.Headers["X-Forwarded-Proto"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(scheme))
        {
            scheme = Request.Scheme;
        }
        var host = Request.Host.Value;
        return $"{scheme}://{host}".TrimEnd('/');
    }

    private IActionResult ETagContent(string content, string contentType, bool noStore = false)
    {
        if (noStore)
        {
            Response.Headers.CacheControl = "no-store";
            return Content(content, contentType, System.Text.Encoding.UTF8);
        }

        var etag = MagHtmlRenderer.ComputeETag(content);
        Response.Headers.ETag = etag;
        Response.Headers.CacheControl = "public, no-cache, must-revalidate";

        if (Request.Headers.IfNoneMatch.Contains(etag))
        {
            return StatusCode(StatusCodes.Status304NotModified);
        }

        return Content(content, contentType, System.Text.Encoding.UTF8);
    }

    private static MagPostCard ToCard(BlogPost p) =>
        new(
            p.Id,
            p.Title,
            p.Slug,
            p.Excerpt,
            p.CoverImageUrl,
            p.CoverImageAlt,
            p.AuthorName,
            p.Category?.Name,
            p.Category?.Slug,
            p.Category?.AccentColor,
            p.Tags,
            p.PublishedAt ?? p.CreatedAt,
            p.LastModifiedAt,
            p.ReadingTimeMinutes,
            p.ViewCount,
            p.IsFeatured);
}
