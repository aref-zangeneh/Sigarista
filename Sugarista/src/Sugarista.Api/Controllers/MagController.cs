using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sugarista.Api.Mag;
using Sugarista.Application.Blog;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Domain.Entities;

namespace Sugarista.Api.Controllers;

[AllowAnonymous]
public class MagController : ControllerBase
{
    private const int DefaultPageSize = 9;
    private readonly IApplicationDbContext _dbContext;
    private readonly IConfiguration _configuration;

    public MagController(IApplicationDbContext dbContext, IConfiguration configuration)
    {
        _dbContext = dbContext;
        _configuration = configuration;
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

        // Non-blocking atomic view count increment (GET only)
        if (!HttpMethods.IsHead(Request.Method))
        {
            try
            {
                await _dbContext.BlogPosts
                    .Where(p => p.Id == post.Id)
                    .ExecuteUpdateAsync(
                        setters => setters.SetProperty(p => p.ViewCount, p => p.ViewCount + 1),
                        cancellationToken);
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

        var postCard = ToCard(post) with { ViewCount = post.ViewCount + 1 };
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

        return CachedContent(html, "text/html; charset=utf-8", maxAgeSeconds: 60);
    }

    [HttpGet("/mag/feed.xml")]
    [HttpHead("/mag/feed.xml")]
    public async Task<IActionResult> RssFeed(CancellationToken cancellationToken = default)
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

        var xml = MagHtmlRenderer.RenderRssFeedXml(site, posts);
        return CachedContent(xml, "application/rss+xml; charset=utf-8", maxAgeSeconds: 300);
    }

    [HttpGet("/sitemap.xml")]
    [HttpHead("/sitemap.xml")]
    public async Task<IActionResult> Sitemap(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var baseUrl = ResolvePublicBaseUrl();

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

        var xml = MagHtmlRenderer.RenderSitemapXml(
            baseUrl,
            categories.Select(c => (c.Slug, c.LastMod)).ToList(),
            posts.Select(p => (p.Slug, p.LastMod, p.CoverImageUrl, p.Title)).ToList());

        return CachedContent(xml, "application/xml; charset=utf-8", maxAgeSeconds: 300);
    }

    [HttpGet("/robots.txt")]
    [HttpHead("/robots.txt")]
    public IActionResult Robots()
    {
        var txt = MagHtmlRenderer.RenderRobotsTxt(ResolvePublicBaseUrl());
        return CachedContent(txt, "text/plain; charset=utf-8", maxAgeSeconds: 3600);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private async Task<IActionResult> RenderArchiveAsync(
        string? categorySlug,
        string? tag,
        string? searchQuery,
        int page,
        CancellationToken cancellationToken)
    {
        var safePage = Math.Max(1, page);
        var now = DateTimeOffset.UtcNow;
        var site = await LoadSiteContextAsync(now, cancellationToken);

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

        var cleanTag = string.IsNullOrWhiteSpace(tag) ? null : Uri.UnescapeDataString(tag).Trim();
        var cleanSearch = string.IsNullOrWhiteSpace(searchQuery) ? null : searchQuery.Trim();

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
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)DefaultPageSize));
        if (safePage > totalPages) safePage = totalPages;

        var pagePosts = await query
            .OrderByDescending(p => p.IsFeatured)
            .ThenBy(p => p.DisplayOrder)
            .ThenByDescending(p => p.PublishedAt ?? p.CreatedAt)
            .Skip((safePage - 1) * DefaultPageSize)
            .Take(DefaultPageSize)
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

        return CachedContent(html, "text/html; charset=utf-8", maxAgeSeconds: cleanSearch is null ? 60 : 0);
    }

    private async Task<MagSiteContext> LoadSiteContextAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var settings = await _dbContext.SiteSettings
            .AsNoTracking()
            .Select(s => new
            {
                s.CafeName,
                s.MetaDescription,
                s.ContactPhone,
                s.Address,
                s.InstagramUrl
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

        return new MagSiteContext(
            ResolvePublicBaseUrl(),
            settings?.CafeName ?? "شوگریستا",
            settings?.MetaDescription ?? string.Empty,
            settings?.ContactPhone,
            settings?.Address,
            settings?.InstagramUrl,
            categories);
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

    private IActionResult CachedContent(string content, string contentType, int maxAgeSeconds)
    {
        var etag = MagHtmlRenderer.ComputeETag(content);
        Response.Headers.ETag = etag;
        Response.Headers.CacheControl = maxAgeSeconds > 0
            ? $"public, max-age={maxAgeSeconds}, stale-while-revalidate={maxAgeSeconds * 5}"
            : "no-store";

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
