using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

public class MenuModifier : BaseAuditableEntity
{
    public MenuModifier()
    {
    }

    public MenuModifier(string persianName, string englishName, int displayOrder, bool isActive = true)
    {
        Update(persianName, englishName, displayOrder, isActive);
    }

    public string Name { get; private set; } = string.Empty;

    public string PersianName { get; private set; } = string.Empty;

    public string EnglishName { get; private set; } = string.Empty;

    public int DisplayOrder { get; private set; }

    public bool IsActive { get; private set; } = true;

    public void Update(string persianName, string englishName, int displayOrder, bool isActive)
    {
        if (string.IsNullOrWhiteSpace(persianName))
        {
            throw new DomainException("Modifier Persian name is required.");
        }

        if (string.IsNullOrWhiteSpace(englishName))
        {
            throw new DomainException("Modifier English name is required.");
        }

        if (displayOrder < 0)
        {
            throw new DomainException("Modifier display order must be greater than or equal to zero.");
        }

        PersianName = persianName.Trim();
        EnglishName = englishName.Trim();
        Name = PersianName;
        DisplayOrder = displayOrder;
        IsActive = isActive;
    }
}

