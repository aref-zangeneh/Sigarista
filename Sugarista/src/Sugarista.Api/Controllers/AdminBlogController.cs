using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sugarista.Api.Authorization;
using Sugarista.Application.Blog;
using Sugarista.Application.Common.Extensions;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.Common.Models;
using Sugarista.Domain.Constants;
using Sugarista.Domain.Entities;

namespace Sugarista.Api.Controllers;

[Authorize]
[Route("api/admin/blog")]
public class AdminBlogController : ApiControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IApplicationDbContext _dbContext;

    public AdminBlogController(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    // ── Categories ──────────────────────────────────────────────────────────

    [HttpGet("categories")]
    [RequirePermission(AdminPermissions.Blog.View)]
    public async Task<ActionResult<IReadOnlyList<AdminBlogCategoryDto>>> GetCategories(CancellationToken cancellationToken)
    {
        var items = await _dbContext.BlogCategories
            .AsNoTracking()
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.Name)
            .Select(c => new AdminBlogCategoryDto(
                c.Id,
                c.Name,
                c.Slug,
                c.Description,
                c.MetaTitle,
                c.MetaDescription,
                c.AccentColor,
                c.DisplayOrder,
                c.IsActive,
                c.Posts.Count))
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpPost("categories")]
    [RequirePermission(AdminPermissions.Blog.Create)]
    public async Task<ActionResult<AdminBlogCategoryDto>> CreateCategory(
        [FromBody] UpsertBlogCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var slug = BlogCategory.NormalizeSlug(string.IsNullOrWhiteSpace(request.Slug) ? request.Name : request.Slug);
        if (await _dbContext.BlogCategories.AnyAsync(c => c.Slug == slug, cancellationToken))
        {
            return Conflict(new { detail = "دسته‌بندی دیگری با این اسلاگ وجود دارد." });
        }

        var category = new BlogCategory(
            request.Name,
            slug,
            request.Description,
            request.MetaTitle,
            request.MetaDescription,
            request.AccentColor,
            request.DisplayOrder,
            request.IsActive);

        _dbContext.BlogCategories.Add(category);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new AdminBlogCategoryDto(
            category.Id,
            category.Name,
            category.Slug,
            category.Description,
            category.MetaTitle,
            category.MetaDescription,
            category.AccentColor,
            category.DisplayOrder,
            category.IsActive,
            0));
    }

    [HttpPut("categories/{id:guid}")]
    [RequirePermission(AdminPermissions.Blog.Update)]
    public async Task<ActionResult<AdminBlogCategoryDto>> UpdateCategory(
        Guid id,
        [FromBody] UpsertBlogCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var category = await _dbContext.BlogCategories
            .Include(c => c.Posts)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (category is null)
        {
            return NotFound();
        }

        var slug = BlogCategory.NormalizeSlug(string.IsNullOrWhiteSpace(request.Slug) ? request.Name : request.Slug);
        if (await _dbContext.BlogCategories.AnyAsync(c => c.Id != id && c.Slug == slug, cancellationToken))
        {
            return Conflict(new { detail = "دسته‌بندی دیگری با این اسلاگ وجود دارد." });
        }

        category.Update(
            request.Name,
            slug,
            request.Description,
            request.MetaTitle,
            request.MetaDescription,
            request.AccentColor,
            request.DisplayOrder,
            request.IsActive);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new AdminBlogCategoryDto(
            category.Id,
            category.Name,
            category.Slug,
            category.Description,
            category.MetaTitle,
            category.MetaDescription,
            category.AccentColor,
            category.DisplayOrder,
            category.IsActive,
            category.Posts.Count));
    }

    [HttpDelete("categories/{id:guid}")]
    [RequirePermission(AdminPermissions.Blog.Delete)]
    public async Task<IActionResult> DeleteCategory(Guid id, CancellationToken cancellationToken)
    {
        var category = await _dbContext.BlogCategories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (category is null)
        {
            return NotFound();
        }

        _dbContext.BlogCategories.Remove(category);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    // ── Posts ───────────────────────────────────────────────────────────────

    [HttpGet("posts")]
    [RequirePermission(AdminPermissions.Blog.View)]
    public async Task<ActionResult<PaginatedList<AdminBlogPostListItemDto>>> GetPosts(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] Guid? categoryId = null,
        CancellationToken cancellationToken = default)
    {
        var pagination = new PaginationQuery(page, pageSize, search);
        var query = _dbContext.BlogPosts.AsNoTracking().Include(p => p.Category).AsQueryable();

        if (!string.IsNullOrWhiteSpace(pagination.Search))
        {
            var term = pagination.Search.Trim().ToLowerInvariant();
            query = query.Where(p =>
                p.Title.ToLower().Contains(term) ||
                p.Slug.ToLower().Contains(term) ||
                (p.FocusKeyword != null && p.FocusKeyword.ToLower().Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<BlogPostStatus>(status, true, out var statusFilter))
        {
            query = query.Where(p => p.Status == statusFilter);
        }

        if (categoryId.HasValue)
        {
            query = query.Where(p => p.BlogCategoryId == categoryId.Value);
        }

        var result = await query
            .OrderByDescending(p => p.IsFeatured)
            .ThenBy(p => p.DisplayOrder)
            .ThenByDescending(p => p.PublishedAt ?? p.CreatedAt)
            .Select(p => new AdminBlogPostListItemDto(
                p.Id,
                p.Title,
                p.Slug,
                p.BlogCategoryId,
                p.Category != null ? p.Category.Name : null,
                p.AuthorName,
                p.Status.ToString(),
                p.PublishedAt,
                p.IsFeatured,
                p.ReadingTimeMinutes,
                p.ViewCount,
                p.DisplayOrder,
                p.FocusKeyword,
                p.NoIndex,
                p.CoverImageUrl,
                p.CreatedAt,
                p.LastModifiedAt))
            .ToPaginatedListAsync(pagination, cancellationToken);

        return Ok(result);
    }

    [HttpGet("posts/{id:guid}")]
    [RequirePermission(AdminPermissions.Blog.View)]
    public async Task<ActionResult<AdminBlogPostDetailDto>> GetPostById(Guid id, CancellationToken cancellationToken)
    {
        var post = await _dbContext.BlogPosts
            .AsNoTracking()
            .Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        return post is null ? NotFound() : Ok(ToDetail(post));
    }

    [HttpPost("posts")]
    [RequirePermission(AdminPermissions.Blog.Create)]
    public async Task<ActionResult<AdminBlogPostDetailDto>> CreatePost(
        [FromBody] UpsertBlogPostRequest request,
        CancellationToken cancellationToken)
    {
        var slug = BlogCategory.NormalizeSlug(string.IsNullOrWhiteSpace(request.Slug) ? request.Title : request.Slug);
        if (string.IsNullOrWhiteSpace(slug))
        {
            return BadRequest(new { detail = "اسلاگ معتبر برای مقاله وارد کنید." });
        }

        if (await _dbContext.BlogPosts.AnyAsync(p => p.Slug == slug, cancellationToken))
        {
            return Conflict(new { detail = "مقاله دیگری با این اسلاگ (آدرس URL) وجود دارد." });
        }

        var status = Enum.TryParse<BlogPostStatus>(request.Status, true, out var parsedStatus)
            ? parsedStatus
            : BlogPostStatus.Draft;

        var readingMinutes = request.ReadingTimeMinutes is > 0
            ? request.ReadingTimeMinutes.Value
            : BlogMarkdownRenderer.EstimateReadingTimeMinutes(request.ContentMarkdown);

        var faqJson = SerializeFaq(request.FaqItems);

        var post = new BlogPost(
            request.Title,
            slug,
            request.ContentMarkdown,
            request.Excerpt,
            request.BlogCategoryId,
            request.CoverImageUrl,
            request.CoverImageAlt,
            request.AuthorName,
            request.Tags,
            status,
            request.PublishedAt,
            request.IsFeatured,
            readingMinutes,
            request.DisplayOrder,
            request.MetaTitle,
            request.MetaDescription,
            request.FocusKeyword,
            request.CanonicalUrl,
            request.NoIndex,
            faqJson,
            request.RelatedMenuItemIds);

        _dbContext.BlogPosts.Add(post);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(ToDetail(post));
    }

    [HttpPut("posts/{id:guid}")]
    [RequirePermission(AdminPermissions.Blog.Update)]
    public async Task<ActionResult<AdminBlogPostDetailDto>> UpdatePost(
        Guid id,
        [FromBody] UpsertBlogPostRequest request,
        CancellationToken cancellationToken)
    {
        var post = await _dbContext.BlogPosts.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (post is null)
        {
            return NotFound();
        }

        var slug = BlogCategory.NormalizeSlug(string.IsNullOrWhiteSpace(request.Slug) ? request.Title : request.Slug);
        if (string.IsNullOrWhiteSpace(slug))
        {
            return BadRequest(new { detail = "اسلاگ معتبر برای مقاله وارد کنید." });
        }

        if (await _dbContext.BlogPosts.AnyAsync(p => p.Id != id && p.Slug == slug, cancellationToken))
        {
            return Conflict(new { detail = "مقاله دیگری با این اسلاگ (آدرس URL) وجود دارد." });
        }

        var status = Enum.TryParse<BlogPostStatus>(request.Status, true, out var parsedStatus)
            ? parsedStatus
            : BlogPostStatus.Draft;

        var readingMinutes = request.ReadingTimeMinutes is > 0
            ? request.ReadingTimeMinutes.Value
            : BlogMarkdownRenderer.EstimateReadingTimeMinutes(request.ContentMarkdown);

        var faqJson = SerializeFaq(request.FaqItems);

        post.Update(
            request.Title,
            slug,
            request.ContentMarkdown,
            request.Excerpt,
            request.BlogCategoryId,
            request.CoverImageUrl,
            request.CoverImageAlt,
            request.AuthorName,
            request.Tags,
            status,
            request.PublishedAt,
            request.IsFeatured,
            readingMinutes,
            request.DisplayOrder,
            request.MetaTitle,
            request.MetaDescription,
            request.FocusKeyword,
            request.CanonicalUrl,
            request.NoIndex,
            faqJson,
            request.RelatedMenuItemIds);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(ToDetail(post));
    }

    [HttpDelete("posts/{id:guid}")]
    [RequirePermission(AdminPermissions.Blog.Delete)]
    public async Task<IActionResult> DeletePost(Guid id, CancellationToken cancellationToken)
    {
        var post = await _dbContext.BlogPosts.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (post is null)
        {
            return NotFound();
        }

        _dbContext.BlogPosts.Remove(post);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static string? SerializeFaq(IReadOnlyCollection<BlogFaqItem>? items)
    {
        if (items is null || items.Count == 0)
        {
            return null;
        }

        var cleaned = items
            .Where(item => !string.IsNullOrWhiteSpace(item.Question) && !string.IsNullOrWhiteSpace(item.Answer))
            .Select(item => new BlogFaqItem(item.Question.Trim(), item.Answer.Trim()))
            .Take(20)
            .ToList();

        return cleaned.Count == 0 ? null : JsonSerializer.Serialize(cleaned, JsonOptions);
    }

    private static AdminBlogPostDetailDto ToDetail(BlogPost p) =>
        new(
            p.Id,
            p.Title,
            p.Slug,
            p.Excerpt,
            p.ContentMarkdown,
            p.BlogCategoryId,
            p.Category?.Name,
            p.CoverImageUrl,
            p.CoverImageAlt,
            p.AuthorName,
            p.Tags,
            p.Status.ToString(),
            p.PublishedAt,
            p.IsFeatured,
            p.ReadingTimeMinutes,
            p.ViewCount,
            p.DisplayOrder,
            p.MetaTitle,
            p.MetaDescription,
            p.FocusKeyword,
            p.CanonicalUrl,
            p.NoIndex,
            BlogMarkdownRenderer.ParseFaqItems(p.FaqJson),
            p.RelatedMenuItemIds,
            p.CreatedAt,
            p.LastModifiedAt);
}

public record UpsertBlogCategoryRequest(
    string Name,
    string? Slug,
    string? Description,
    string? MetaTitle,
    string? MetaDescription,
    string? AccentColor,
    int DisplayOrder = 0,
    bool IsActive = true);

public record AdminBlogCategoryDto(
    Guid Id,
    string Name,
    string Slug,
    string Description,
    string? MetaTitle,
    string? MetaDescription,
    string? AccentColor,
    int DisplayOrder,
    bool IsActive,
    int PostCount);

public record UpsertBlogPostRequest(
    string Title,
    string? Slug,
    string ContentMarkdown,
    string? Excerpt = null,
    Guid? BlogCategoryId = null,
    string? CoverImageUrl = null,
    string? CoverImageAlt = null,
    string? AuthorName = null,
    IReadOnlyCollection<string>? Tags = null,
    string Status = "Draft",
    DateTimeOffset? PublishedAt = null,
    bool IsFeatured = false,
    int? ReadingTimeMinutes = null,
    int DisplayOrder = 0,
    string? MetaTitle = null,
    string? MetaDescription = null,
    string? FocusKeyword = null,
    string? CanonicalUrl = null,
    bool NoIndex = false,
    IReadOnlyCollection<BlogFaqItem>? FaqItems = null,
    IReadOnlyCollection<Guid>? RelatedMenuItemIds = null);

public record AdminBlogPostListItemDto(
    Guid Id,
    string Title,
    string Slug,
    Guid? BlogCategoryId,
    string? CategoryName,
    string AuthorName,
    string Status,
    DateTimeOffset? PublishedAt,
    bool IsFeatured,
    int ReadingTimeMinutes,
    long ViewCount,
    int DisplayOrder,
    string? FocusKeyword,
    bool NoIndex,
    string? CoverImageUrl,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastModifiedAt);

public record AdminBlogPostDetailDto(
    Guid Id,
    string Title,
    string Slug,
    string Excerpt,
    string ContentMarkdown,
    Guid? BlogCategoryId,
    string? CategoryName,
    string? CoverImageUrl,
    string? CoverImageAlt,
    string AuthorName,
    IReadOnlyList<string> Tags,
    string Status,
    DateTimeOffset? PublishedAt,
    bool IsFeatured,
    int ReadingTimeMinutes,
    long ViewCount,
    int DisplayOrder,
    string? MetaTitle,
    string? MetaDescription,
    string? FocusKeyword,
    string? CanonicalUrl,
    bool NoIndex,
    IReadOnlyList<BlogFaqItem> FaqItems,
    IReadOnlyList<Guid> RelatedMenuItemIds,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastModifiedAt);
