using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

public class MenuItem : BaseAuditableEntity
{
    public MenuItem()
    {
    }

    public MenuItem(
        Guid categoryId,
        string name,
        string description,
        int price,
        IEnumerable<string> tags,
        string? badge,
        bool featured,
        string accent,
        string shortLabel,
        int displayOrder,
        bool isActive = true)
    {
        Update(categoryId, name, description, price, tags, badge, featured, accent, shortLabel, displayOrder, isActive);
    }

    public Guid CategoryId { get; private set; }

    public Category Category { get; private set; } = null!;

    public string Name { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public int Price { get; private set; }

    public string[] Tags { get; private set; } = [];

    public string? Badge { get; private set; }

    public bool Featured { get; private set; }

    public string Accent { get; private set; } = string.Empty;

    public string ShortLabel { get; private set; } = string.Empty;

    public int DisplayOrder { get; private set; }

    public bool IsActive { get; private set; } = true;

    public void Update(
        Guid categoryId,
        string name,
        string description,
        int price,
        IEnumerable<string> tags,
        string? badge,
        bool featured,
        string accent,
        string shortLabel,
        int displayOrder,
        bool isActive)
    {
        if (categoryId == Guid.Empty)
        {
            throw new DomainException("Menu item category is required.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Menu item name is required.");
        }

        if (price < 0)
        {
            throw new DomainException("Menu item price must be greater than or equal to zero.");
        }

        CategoryId = categoryId;
        Name = name.Trim();
        Description = description.Trim();
        Price = price;
        Tags = tags
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        Badge = string.IsNullOrWhiteSpace(badge) ? null : badge.Trim();
        Featured = featured;
        Accent = string.IsNullOrWhiteSpace(accent) ? "linear-gradient(135deg, #5b3425, #8a5339)" : accent.Trim();
        ShortLabel = string.IsNullOrWhiteSpace(shortLabel) ? Name[..Math.Min(2, Name.Length)] : shortLabel.Trim();
        DisplayOrder = displayOrder;
        IsActive = isActive;
    }
}
