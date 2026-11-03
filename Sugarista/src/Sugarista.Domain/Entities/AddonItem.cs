using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

public class AddonItem : BaseAuditableEntity
{
    public AddonItem()
    {
    }

    public AddonItem(
        string persianName,
        string englishName,
        string description,
        decimal price,
        IEnumerable<string> tags,
        int displayOrder,
        bool isActive = true,
        decimal? specialOfferPrice = null)
    {
        Update(persianName, englishName, description, price, tags, displayOrder, isActive, specialOfferPrice);
    }

    public string Name { get; private set; } = string.Empty;

    public string PersianName { get; private set; } = string.Empty;

    public string EnglishName { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public decimal Price { get; private set; }

    /// <summary>Discounted price shown as an upsell suggestion (null = no offer).</summary>
    public decimal? SpecialOfferPrice { get; private set; }

    public string[] Tags { get; private set; } = [];

    public int DisplayOrder { get; private set; }

    public bool IsActive { get; private set; } = true;

    public void Update(
        string persianName,
        string englishName,
        string description,
        decimal price,
        IEnumerable<string> tags,
        int displayOrder,
        bool isActive,
        decimal? specialOfferPrice = null)
    {
        if (string.IsNullOrWhiteSpace(persianName))
        {
            throw new DomainException("Addon Persian name is required.");
        }

        if (string.IsNullOrWhiteSpace(englishName))
        {
            throw new DomainException("Addon English name is required.");
        }

        if (price < 0)
        {
            throw new DomainException("Addon price must be greater than or equal to zero.");
        }

        if (specialOfferPrice is < 0)
        {
            throw new DomainException("Addon special offer price cannot be negative.");
        }

        if (specialOfferPrice.HasValue && specialOfferPrice.Value > price)
        {
            throw new DomainException("Addon special offer price cannot exceed its price.");
        }

        PersianName = persianName.Trim();
        EnglishName = englishName.Trim();
        Name = PersianName;
        Description = description.Trim();
        Price = price;
        Tags = NormalizeList(tags);
        DisplayOrder = displayOrder;
        IsActive = isActive;
        SpecialOfferPrice = specialOfferPrice;
    }

    private static string[] NormalizeList(IEnumerable<string> values)
    {
        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}

