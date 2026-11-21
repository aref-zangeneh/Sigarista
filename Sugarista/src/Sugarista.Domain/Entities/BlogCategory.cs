using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

public class BlogCategory : BaseAuditableEntity
{
    private BlogCategory()
    {
    }

    public BlogCategory(
        string name,
        string slug,
        string? description = null,
        string? metaTitle = null,
        string? metaDescription = null,
        string? accentColor = null,
        int displayOrder = 0,
        bool isActive = true)
    {
        Update(name, slug, description, metaTitle, metaDescription, accentColor, displayOrder, isActive);
    }

    public string Name { get; private set; } = string.Empty;

    public string Slug { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public string? MetaTitle { get; private set; }

    public string? MetaDescription { get; private set; }

    public string? AccentColor { get; private set; }

    public int DisplayOrder { get; private set; }

    public bool IsActive { get; private set; } = true;

    public ICollection<BlogPost> Posts { get; private set; } = [];

    public void Update(
        string name,
        string slug,
        string? description,
        string? metaTitle,
        string? metaDescription,
        string? accentColor,
        int displayOrder,
        bool isActive)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("نام دسته‌بندی مجله الزامی است.");
        }

        var normalizedSlug = NormalizeSlug(slug);
        if (string.IsNullOrWhiteSpace(normalizedSlug))
        {
            throw new DomainException("اسلاگ دسته‌بندی مجله الزامی است.");
        }

        Name = name.Trim();
        Slug = normalizedSlug;
        Description = string.IsNullOrWhiteSpace(description) ? string.Empty : description.Trim();
        MetaTitle = string.IsNullOrWhiteSpace(metaTitle) ? null : metaTitle.Trim();
        MetaDescription = string.IsNullOrWhiteSpace(metaDescription) ? null : metaDescription.Trim();
        AccentColor = string.IsNullOrWhiteSpace(accentColor) ? null : accentColor.Trim();
        DisplayOrder = Math.Max(0, displayOrder);
        IsActive = isActive;
    }

    public static string NormalizeSlug(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var chars = input.Trim().ToLowerInvariant();
        var sb = new System.Text.StringBuilder(chars.Length);
        var lastWasDash = false;

        foreach (var ch in chars)
        {
            if (char.IsLetterOrDigit(ch))
            {
                sb.Append(ch);
                lastWasDash = false;
            }
            else if (ch is ' ' or '-' or '_' or '/' or '\u200C')
            {
                if (!lastWasDash && sb.Length > 0)
                {
                    sb.Append('-');
                    lastWasDash = true;
                }
            }
        }

        return sb.ToString().Trim('-');
    }
}
