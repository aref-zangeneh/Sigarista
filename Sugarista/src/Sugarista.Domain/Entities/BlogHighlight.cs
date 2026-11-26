using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

public class BlogHighlight : BaseAuditableEntity
{
    private static readonly HashSet<string> AllowedThemes = new(StringComparer.OrdinalIgnoreCase)
    {
        "teal",
        "magenta",
        "gold"
    };

    private BlogHighlight()
    {
    }

    public BlogHighlight(
        string title,
        string subtitle,
        string? badgeText = null,
        string? iconText = null,
        string? imageUrl = null,
        string? linkUrl = null,
        string? linkLabel = null,
        string? themeStyle = "teal",
        int displayOrder = 0,
        bool isActive = true)
    {
        Update(
            title,
            subtitle,
            badgeText,
            iconText,
            imageUrl,
            linkUrl,
            linkLabel,
            themeStyle,
            displayOrder,
            isActive);
    }

    public string Title { get; private set; } = string.Empty;

    public string Subtitle { get; private set; } = string.Empty;

    public string BadgeText { get; private set; } = string.Empty;

    public string IconText { get; private set; } = "☕";

    public string? ImageUrl { get; private set; }

    public string? LinkUrl { get; private set; }

    public string? LinkLabel { get; private set; }

    public string ThemeStyle { get; private set; } = "teal";

    public int DisplayOrder { get; private set; }

    public bool IsActive { get; private set; } = true;

    public void Update(
        string title,
        string subtitle,
        string? badgeText,
        string? iconText,
        string? imageUrl,
        string? linkUrl,
        string? linkLabel,
        string? themeStyle,
        int displayOrder,
        bool isActive)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new DomainException("عنوان کارت ویژه الزامی است.");
        }

        if (string.IsNullOrWhiteSpace(subtitle))
        {
            throw new DomainException("توضیح کوتاه کارت ویژه الزامی است.");
        }

        Title = title.Trim();
        Subtitle = subtitle.Trim();
        BadgeText = string.IsNullOrWhiteSpace(badgeText) ? string.Empty : badgeText.Trim();
        IconText = string.IsNullOrWhiteSpace(iconText) ? "☕" : iconText.Trim();
        ImageUrl = string.IsNullOrWhiteSpace(imageUrl) ? null : imageUrl.Trim();
        LinkUrl = string.IsNullOrWhiteSpace(linkUrl) ? null : linkUrl.Trim();
        LinkLabel = string.IsNullOrWhiteSpace(linkLabel) ? null : linkLabel.Trim();

        var cleanTheme = string.IsNullOrWhiteSpace(themeStyle) ? "teal" : themeStyle.Trim().ToLowerInvariant();
        ThemeStyle = AllowedThemes.Contains(cleanTheme) ? cleanTheme : "teal";

        DisplayOrder = Math.Max(0, displayOrder);
        IsActive = isActive;
    }
}
