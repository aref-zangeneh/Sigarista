using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

public class Category : BaseAuditableEntity
{
    private readonly List<MenuItem> _menuItems = [];

    public Category()
    {
    }

    public Category(string slug, string name, int displayOrder, bool isActive = true, string? imageUrl = null)
    {
        Update(slug, name, displayOrder, isActive, imageUrl);
    }

    public string Slug { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    /// <summary>Small picture shown on the customer-facing menu category tabs. Null = text-only tab.</summary>
    public string? ImageUrl { get; private set; }

    public int DisplayOrder { get; private set; }

    public bool IsActive { get; private set; } = true;

    public IReadOnlyCollection<MenuItem> MenuItems => _menuItems.AsReadOnly();

    public void Update(string slug, string name, int displayOrder, bool isActive, string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            throw new DomainException("Category slug is required.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Category name is required.");
        }

        Slug = slug.Trim();
        Name = name.Trim();
        ImageUrl = string.IsNullOrWhiteSpace(imageUrl) ? null : imageUrl.Trim();
        DisplayOrder = displayOrder;
        IsActive = isActive;
    }
}
