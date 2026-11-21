using Sugarista.Domain.Entities;

namespace Sugarista.Domain.UnitTests;

public class BlogTests
{
    [Theory]
    [InlineData("Complete Coffee Guide!", "complete-coffee-guide")]
    [InlineData("  راهنمای کامل قهوه  ", "راهنمای-کامل-قهوه")]
    [InlineData("V60 / Chemex — دمی", "v60-chemex-دمی")]
    [InlineData("قهوه   اسپرسو___عربیکا", "قهوه-اسپرسو-عربیکا")]
    public void NormalizeSlug_HandlesLatinAndPersianSlugs(string input, string expected)
    {
        var result = BlogCategory.NormalizeSlug(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void BlogPost_IsLiveAt_RespectsStatusAndSchedule()
    {
        var now = new DateTimeOffset(2026, 11, 21, 10, 0, 0, TimeSpan.Zero);

        var draft = new BlogPost(
            "راهنمای قهوه",
            "coffee-guide",
            "## مقدمه\nمتن مقاله",
            excerpt: "خلاصه کوتاه مقاله قهوه",
            status: BlogPostStatus.Draft);

        Assert.False(draft.IsLiveAt(now));

        var published = new BlogPost(
            "راهنمای قهوه",
            "coffee-guide-2",
            "## مقدمه\nمتن",
            excerpt: "خلاصه کوتاه",
            status: BlogPostStatus.Published,
            publishedAt: now.AddMinutes(-5));

        Assert.True(published.IsLiveAt(now));

        var futureScheduled = new BlogPost(
            "راهنمای آینده",
            "coffee-future",
            "متن",
            excerpt: "خلاصه",
            status: BlogPostStatus.Scheduled,
            publishedAt: now.AddHours(2));

        Assert.False(futureScheduled.IsLiveAt(now));
        Assert.True(futureScheduled.IsLiveAt(now.AddHours(3)));
    }

    [Fact]
    public void BlogPost_SanitizesTagsAndRelatedMenuItemIds()
    {
        var item1 = Guid.NewGuid();
        var item2 = Guid.NewGuid();

        var post = new BlogPost(
            "اسپرسو چیست",
            "what-is-espresso",
            "متن",
            excerpt: "خلاصه",
            tags: new[] { " قهوه ", "اسپرسو", "قهوه", "", "   " },
            relatedMenuItemIds: new[] { item1, Guid.Empty, item1, item2 });

        Assert.Equal(2, post.Tags.Count);
        Assert.Contains("قهوه", post.Tags);
        Assert.Contains("اسپرسو", post.Tags);

        Assert.Equal(2, post.RelatedMenuItemIds.Count);
        Assert.Equal(item1, post.RelatedMenuItemIds[0]);
        Assert.Equal(item2, post.RelatedMenuItemIds[1]);
    }

    [Fact]
    public void BlogPost_ClampsReadingTimeAndSetsPublishedAtAutomatically()
    {
        var post = new BlogPost(
            "تست",
            "test-slug",
            "متن",
            excerpt: "خلاصه",
            status: BlogPostStatus.Published,
            readingTimeMinutes: 999);

        Assert.Equal(120, post.ReadingTimeMinutes);
        Assert.NotNull(post.PublishedAt);
    }
}
