using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sugarista.Domain.Entities;

namespace Sugarista.Infrastructure.Data.Configurations;

public class BlogCategoryConfiguration : IEntityTypeConfiguration<BlogCategory>
{
    public void Configure(EntityTypeBuilder<BlogCategory> builder)
    {
        builder.Property(c => c.Name).HasMaxLength(120).IsRequired();
        builder.Property(c => c.Slug).HasMaxLength(120).IsRequired();
        builder.Property(c => c.Description).HasMaxLength(500).IsRequired();
        builder.Property(c => c.MetaTitle).HasMaxLength(160);
        builder.Property(c => c.MetaDescription).HasMaxLength(320);
        builder.Property(c => c.AccentColor).HasMaxLength(30);

        builder.HasIndex(c => c.Slug).IsUnique();
        builder.HasIndex(c => c.DisplayOrder);
        builder.HasIndex(c => c.IsActive);

        builder.ToTable("BlogCategories");
    }
}

public class BlogPostConfiguration : IEntityTypeConfiguration<BlogPost>
{
    public void Configure(EntityTypeBuilder<BlogPost> builder)
    {
        builder.Property(p => p.Title).HasMaxLength(220).IsRequired();
        builder.Property(p => p.Slug).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Excerpt).HasMaxLength(600).IsRequired();
        builder.Property(p => p.ContentMarkdown).IsRequired();
        builder.Property(p => p.CoverImageUrl).HasMaxLength(500);
        builder.Property(p => p.CoverImageAlt).HasMaxLength(240);
        builder.Property(p => p.AuthorName).HasMaxLength(120).IsRequired();
        builder.Property(p => p.Tags).HasColumnType("text[]");
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(p => p.MetaTitle).HasMaxLength(160);
        builder.Property(p => p.MetaDescription).HasMaxLength(320);
        builder.Property(p => p.FocusKeyword).HasMaxLength(120);
        builder.Property(p => p.CanonicalUrl).HasMaxLength(500);
        builder.Property(p => p.RelatedMenuItemIds).HasColumnType("uuid[]");

        builder.HasOne(p => p.Category)
            .WithMany(c => c.Posts)
            .HasForeignKey(p => p.BlogCategoryId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(p => p.Slug).IsUnique();
        builder.HasIndex(p => p.Status);
        builder.HasIndex(p => p.PublishedAt);
        builder.HasIndex(p => p.IsFeatured);
        builder.HasIndex(p => p.DisplayOrder);
        builder.HasIndex(p => p.BlogCategoryId);

        builder.ToTable("BlogPosts");
    }
}

public class BlogHighlightConfiguration : IEntityTypeConfiguration<BlogHighlight>
{
    public void Configure(EntityTypeBuilder<BlogHighlight> builder)
    {
        builder.Property(h => h.Title).HasMaxLength(160).IsRequired();
        builder.Property(h => h.Subtitle).HasMaxLength(400).IsRequired();
        builder.Property(h => h.BadgeText).HasMaxLength(60).IsRequired();
        builder.Property(h => h.IconText).HasMaxLength(16).IsRequired();
        builder.Property(h => h.ImageUrl).HasMaxLength(500);
        builder.Property(h => h.LinkUrl).HasMaxLength(400);
        builder.Property(h => h.LinkLabel).HasMaxLength(80);
        builder.Property(h => h.ThemeStyle).HasMaxLength(20).IsRequired();

        builder.HasIndex(h => h.DisplayOrder);
        builder.HasIndex(h => h.IsActive);

        builder.ToTable("BlogHighlights");
    }
}
