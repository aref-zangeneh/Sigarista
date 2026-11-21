using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

public enum BlogPostStatus
{
    Draft = 0,
    Published = 1,
    Scheduled = 2
}

public class BlogPost : BaseAuditableEntity
{
    private BlogPost()
    {
    }

    public BlogPost(
        string title,
        string slug,
        string contentMarkdown,
        string? excerpt = null,
        Guid? blogCategoryId = null,
        string? coverImageUrl = null,
        string? coverImageAlt = null,
        string? authorName = null,
        IEnumerable<string>? tags = null,
        BlogPostStatus status = BlogPostStatus.Draft,
        DateTimeOffset? publishedAt = null,
        bool isFeatured = false,
        int readingTimeMinutes = 1,
        int displayOrder = 0,
        string? metaTitle = null,
        string? metaDescription = null,
        string? focusKeyword = null,
        string? canonicalUrl = null,
        bool noIndex = false,
        string? faqJson = null,
        IEnumerable<Guid>? relatedMenuItemIds = null)
    {
        Update(
            title,
            slug,
            contentMarkdown,
            excerpt,
            blogCategoryId,
            coverImageUrl,
            coverImageAlt,
            authorName,
            tags,
            status,
            publishedAt,
            isFeatured,
            readingTimeMinutes,
            displayOrder,
            metaTitle,
            metaDescription,
            focusKeyword,
            canonicalUrl,
            noIndex,
            faqJson,
            relatedMenuItemIds);
    }

    public Guid? BlogCategoryId { get; private set; }

    public BlogCategory? Category { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string Slug { get; private set; } = string.Empty;

    public string Excerpt { get; private set; } = string.Empty;

    public string ContentMarkdown { get; private set; } = string.Empty;

    public string? CoverImageUrl { get; private set; }

    public string? CoverImageAlt { get; private set; }

    public string AuthorName { get; private set; } = "تحریریه شوگریستا";

    public List<string> Tags { get; private set; } = [];

    public BlogPostStatus Status { get; private set; } = BlogPostStatus.Draft;

    public DateTimeOffset? PublishedAt { get; private set; }

    public bool IsFeatured { get; private set; }

    public int ReadingTimeMinutes { get; private set; } = 1;

    public long ViewCount { get; private set; }

    public int DisplayOrder { get; private set; }

    public string? MetaTitle { get; private set; }

    public string? MetaDescription { get; private set; }

    public string? FocusKeyword { get; private set; }

    public string? CanonicalUrl { get; private set; }

    public bool NoIndex { get; private set; }

    public string? FaqJson { get; private set; }

    public List<Guid> RelatedMenuItemIds { get; private set; } = [];

    public bool IsLiveAt(DateTimeOffset now) =>
        Status == BlogPostStatus.Published ||
        (Status == BlogPostStatus.Scheduled && PublishedAt.HasValue && PublishedAt.Value <= now);

    public void Update(
        string title,
        string slug,
        string contentMarkdown,
        string? excerpt,
        Guid? blogCategoryId,
        string? coverImageUrl,
        string? coverImageAlt,
        string? authorName,
        IEnumerable<string>? tags,
        BlogPostStatus status,
        DateTimeOffset? publishedAt,
        bool isFeatured,
        int readingTimeMinutes,
        int displayOrder,
        string? metaTitle,
        string? metaDescription,
        string? focusKeyword,
        string? canonicalUrl,
        bool noIndex,
        string? faqJson,
        IEnumerable<Guid>? relatedMenuItemIds)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new DomainException("عنوان مقاله الزامی است.");
        }

        var normalizedSlug = BlogCategory.NormalizeSlug(slug);
        if (string.IsNullOrWhiteSpace(normalizedSlug))
        {
            throw new DomainException("اسلاگ مقاله الزامی است.");
        }

        if (string.IsNullOrWhiteSpace(contentMarkdown))
        {
            throw new DomainException("متن مقاله الزامی است.");
        }

        if (status == BlogPostStatus.Scheduled && !publishedAt.HasValue)
        {
            throw new DomainException("برای مقاله زمان‌بندی‌شده تعیین تاریخ انتشار الزامی است.");
        }

        Title = title.Trim();
        Slug = normalizedSlug;
        ContentMarkdown = contentMarkdown.Trim();
        Excerpt = string.IsNullOrWhiteSpace(excerpt) ? string.Empty : excerpt.Trim();
        BlogCategoryId = blogCategoryId;
        CoverImageUrl = string.IsNullOrWhiteSpace(coverImageUrl) ? null : coverImageUrl.Trim();
        CoverImageAlt = string.IsNullOrWhiteSpace(coverImageAlt) ? null : coverImageAlt.Trim();
        AuthorName = string.IsNullOrWhiteSpace(authorName) ? "تحریریه شوگریستا" : authorName.Trim();
        Tags = (tags ?? [])
            .Select(tag => tag?.Trim() ?? string.Empty)
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(15)
            .ToList();
        Status = status;
        PublishedAt = status switch
        {
            BlogPostStatus.Published => (publishedAt ?? PublishedAt ?? DateTimeOffset.UtcNow).ToUniversalTime(),
            BlogPostStatus.Scheduled => publishedAt?.ToUniversalTime(),
            _ => publishedAt?.ToUniversalTime()
        };
        IsFeatured = isFeatured;
        ReadingTimeMinutes = Math.Clamp(readingTimeMinutes, 1, 120);
        DisplayOrder = Math.Max(0, displayOrder);
        MetaTitle = string.IsNullOrWhiteSpace(metaTitle) ? null : metaTitle.Trim();
        MetaDescription = string.IsNullOrWhiteSpace(metaDescription) ? null : metaDescription.Trim();
        FocusKeyword = string.IsNullOrWhiteSpace(focusKeyword) ? null : focusKeyword.Trim();
        CanonicalUrl = string.IsNullOrWhiteSpace(canonicalUrl) ? null : canonicalUrl.Trim();
        NoIndex = noIndex;
        FaqJson = string.IsNullOrWhiteSpace(faqJson) ? null : faqJson.Trim();
        RelatedMenuItemIds = (relatedMenuItemIds ?? [])
            .Where(id => id != Guid.Empty)
            .Distinct()
            .Take(8)
            .ToList();
    }
}
