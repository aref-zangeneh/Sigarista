using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

public class Category : BaseAuditableEntity
{
    private readonly List<MenuItem> _menuItems = [];

    public Category()
    {
    }

    public Category(string slug, string name, int displayOrder, bool isActive = true)
    {
        Update(slug, name, displayOrder, isActive);
    }

    public string Slug { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public int DisplayOrder { get; private set; }

    public bool IsActive { get; private set; } = true;

    public IReadOnlyCollection<MenuItem> MenuItems => _menuItems.AsReadOnly();

    public void Update(string slug, string name, int displayOrder, bool isActive)
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
        DisplayOrder = displayOrder;
        IsActive = isActive;
    }
}
