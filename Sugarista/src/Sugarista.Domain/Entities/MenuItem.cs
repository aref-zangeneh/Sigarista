using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

public class MenuItem : BaseAuditableEntity
{
    private const string DefaultAccent = "linear-gradient(135deg, #5b3425, #8a5339)";

    public MenuItem()
    {
    }

    public MenuItem(
        Guid categoryId,
        string persianName,
        string englishName,
        string description,
        string? imageUrl,
        IEnumerable<string> galleryImageUrls,
        int? specialOfferPrice,
        decimal taxPercent,
        bool isOrderable,
        int? preparingDurationMinutes,
        int? calories,
        IEnumerable<string> allergens,
        string? volume,
        int? smallPrice,
        int? smallSpecialOfferPrice,
        string? smallVolume,
        int? mediumPrice,
        int? mediumSpecialOfferPrice,
        string? mediumVolume,
        int? largePrice,
        int? largeSpecialOfferPrice,
        string? largeVolume,
        IEnumerable<string> tags,
        string? badge,
        bool featured,
        string accent,
        string shortLabel,
        int displayOrder,
        bool isActive = true)
    {
        Update(
            categoryId,
            persianName,
            englishName,
            description,
            imageUrl,
            galleryImageUrls,
            specialOfferPrice,
            taxPercent,
            isOrderable,
            preparingDurationMinutes,
            calories,
            allergens,
            volume,
            smallPrice,
            smallSpecialOfferPrice,
            smallVolume,
            mediumPrice,
            mediumSpecialOfferPrice,
            mediumVolume,
            largePrice,
            largeSpecialOfferPrice,
            largeVolume,
            tags,
            badge,
            featured,
            accent,
            shortLabel,
            displayOrder,
            isActive);
    }

    public Guid CategoryId { get; private set; }

    public Category Category { get; private set; } = null!;

    public string Name { get; private set; } = string.Empty;

    public string PersianName { get; private set; } = string.Empty;

    public string EnglishName { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public int Price { get; private set; }

    public string? ImageUrl { get; private set; }

    public string[] GalleryImageUrls { get; private set; } = [];

    public int? SpecialOfferPrice { get; private set; }

    public decimal TaxPercent { get; private set; }

    public bool IsOrderable { get; private set; } = true;

    public int? PreparingDurationMinutes { get; private set; }

    public int? Calories { get; private set; }

    public string[] Allergens { get; private set; } = [];

    public string? Volume { get; private set; }

    public int? SmallPrice { get; private set; }

    public int? SmallSpecialOfferPrice { get; private set; }

    public string? SmallVolume { get; private set; }

    public int? MediumPrice { get; private set; }

    public int? MediumSpecialOfferPrice { get; private set; }

    public string? MediumVolume { get; private set; }

    public int? LargePrice { get; private set; }

    public int? LargeSpecialOfferPrice { get; private set; }

    public string? LargeVolume { get; private set; }

    public string[] Tags { get; private set; } = [];

    public string? Badge { get; private set; }

    public bool Featured { get; private set; }

    public string Accent { get; private set; } = string.Empty;

    public string ShortLabel { get; private set; } = string.Empty;

    public int DisplayOrder { get; private set; }

    public bool IsActive { get; private set; } = true;

    public ICollection<RecipeItem> RecipeItems { get; private set; } = [];

    public void Update(
        Guid categoryId,
        string persianName,
        string englishName,
        string description,
        string? imageUrl,
        IEnumerable<string> galleryImageUrls,
        int? specialOfferPrice,
        decimal taxPercent,
        bool isOrderable,
        int? preparingDurationMinutes,
        int? calories,
        IEnumerable<string> allergens,
        string? volume,
        int? smallPrice,
        int? smallSpecialOfferPrice,
        string? smallVolume,
        int? mediumPrice,
        int? mediumSpecialOfferPrice,
        string? mediumVolume,
        int? largePrice,
        int? largeSpecialOfferPrice,
        string? largeVolume,
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

        if (string.IsNullOrWhiteSpace(persianName))
        {
            throw new DomainException("Menu item Persian name is required.");
        }

        if (string.IsNullOrWhiteSpace(englishName))
        {
            throw new DomainException("Menu item English name is required.");
        }

        ValidateOptionalMoney(specialOfferPrice, "Special offer price");
        ValidateOptionalMoney(smallPrice, "Small price");
        ValidateOptionalMoney(smallSpecialOfferPrice, "Small special offer price");
        ValidateOptionalMoney(mediumPrice, "Medium price");
        ValidateOptionalMoney(mediumSpecialOfferPrice, "Medium special offer price");
        ValidateOptionalMoney(largePrice, "Large price");
        ValidateOptionalMoney(largeSpecialOfferPrice, "Large special offer price");

        if (taxPercent < 0)
        {
            throw new DomainException("Menu item tax percent must be greater than or equal to zero.");
        }

        if (preparingDurationMinutes < 0)
        {
            throw new DomainException("Preparing duration must be greater than or equal to zero.");
        }

        if (calories < 0)
        {
            throw new DomainException("Calories must be greater than or equal to zero.");
        }

        if (!smallPrice.HasValue && !mediumPrice.HasValue && !largePrice.HasValue)
        {
            throw new DomainException("At least one size price is required.");
        }

        CategoryId = categoryId;
        PersianName = persianName.Trim();
        EnglishName = englishName.Trim();
        Name = PersianName;
        Description = description.Trim();
        ImageUrl = NormalizeOptionalText(imageUrl);
        GalleryImageUrls = NormalizeList(galleryImageUrls);
        SmallPrice = smallPrice;
        SmallSpecialOfferPrice = smallSpecialOfferPrice;
        SmallVolume = NormalizeOptionalText(smallVolume);
        MediumPrice = mediumPrice;
        MediumSpecialOfferPrice = mediumSpecialOfferPrice;
        MediumVolume = NormalizeOptionalText(mediumVolume);
        LargePrice = largePrice;
        LargeSpecialOfferPrice = largeSpecialOfferPrice;
        LargeVolume = NormalizeOptionalText(largeVolume);
        Price = mediumPrice ?? smallPrice ?? largePrice ?? 0;
        SpecialOfferPrice = specialOfferPrice ?? mediumSpecialOfferPrice ?? smallSpecialOfferPrice ?? largeSpecialOfferPrice;
        TaxPercent = taxPercent;
        IsOrderable = isOrderable;
        PreparingDurationMinutes = preparingDurationMinutes;
        Calories = calories;
        Allergens = NormalizeList(allergens);
        Volume = NormalizeOptionalText(volume);
        Tags = NormalizeList(tags);
        Badge = string.IsNullOrWhiteSpace(badge) ? null : badge.Trim();
        Featured = featured;
        Accent = string.IsNullOrWhiteSpace(accent) ? DefaultAccent : accent.Trim();
        ShortLabel = string.IsNullOrWhiteSpace(shortLabel) ? PersianName[..Math.Min(2, PersianName.Length)] : shortLabel.Trim();
        DisplayOrder = displayOrder;
        IsActive = isActive;
    }

    private static void ValidateOptionalMoney(int? value, string fieldName)
    {
        if (value < 0)
        {
            throw new DomainException($"{fieldName} must be greater than or equal to zero.");
        }
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
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
