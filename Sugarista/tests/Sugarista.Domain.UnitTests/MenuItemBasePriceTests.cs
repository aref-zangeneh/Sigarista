using Sugarista.Domain.Entities;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.UnitTests.MenuItemTests;

/// <summary>
/// Variant-less menu items (e.g. a 35–40ml espresso with no size options)
/// must be creatable with a base price only — the old rule required pricing
/// at least one size.
/// </summary>
public class MenuItemBasePriceTests
{
    private static MenuItem CreateItem(decimal? price, decimal? small = null, decimal? medium = null, decimal? large = null)
    {
        return new MenuItem(
            Guid.NewGuid(),
            "اسپرسو",
            "Espresso",
            "قهوهٔ خالص",
            imageUrl: null,
            galleryImageUrls: [],
            specialOfferPrice: null,
            taxPercent: 0,
            isOrderable: true,
            preparingDurationMinutes: 3,
            calories: 5,
            allergens: [],
            volume: "35–40 ml",
            smallPrice: small,
            smallSpecialOfferPrice: null,
            smallVolume: null,
            smallName: null,
            mediumPrice: medium,
            mediumSpecialOfferPrice: null,
            mediumVolume: null,
            mediumName: null,
            largePrice: large,
            largeSpecialOfferPrice: null,
            largeVolume: null,
            largeName: null,
            tags: [],
            badge: null,
            featured: false,
            accent: "",
            shortLabel: "اس",
            displayOrder: 1,
            isActive: true,
            price: price);
    }

    [Fact]
    public void VariantLessItem_WithBasePrice_IsAllowed()
    {
        var item = CreateItem(price: 120_000);

        Assert.Equal(120_000, item.Price);
        Assert.Null(item.SmallPrice);
        Assert.Null(item.MediumPrice);
        Assert.Null(item.LargePrice);
    }

    [Fact]
    public void VariantLessItem_WithoutAnyPrice_IsSavedUnpricedAndNotOrderable()
    {
        // Prices are optional at definition time; an unpriced item is saved with
        // Price = 0 and ordering is automatically switched off.
        var unpriced = CreateItem(price: null);
        Assert.Equal(0, unpriced.Price);
        Assert.False(unpriced.IsOrderable);

        var zeroPriced = CreateItem(price: 0);
        Assert.Equal(0, zeroPriced.Price);
        Assert.False(zeroPriced.IsOrderable);
    }

    [Fact]
    public void SizedItem_WithoutBasePrice_IsStillAllowed()
    {
        var item = CreateItem(price: null, medium: 90_000);

        Assert.Equal(90_000, item.Price);
    }

    [Fact]
    public void SizedItem_WithBasePrice_KeepsSizeDerivedPrice()
    {
        // base price provided but sizes exist → sizes still define Price
        var item = CreateItem(price: 200_000, small: 80_000, medium: 90_000, large: 100_000);

        Assert.Equal(200_000, item.Price);
    }

    [Fact]
    public void Update_CanAddBasePriceToSizedItem_AndLaterRemoveSizes()
    {
        var item = CreateItem(price: null, medium: 90_000);

        item.Update(
            item.CategoryId,
            item.PersianName,
            item.EnglishName,
            item.Description,
            item.ImageUrl,
            item.GalleryImageUrls,
            item.SpecialOfferPrice,
            item.TaxPercent,
            item.IsOrderable,
            item.PreparingDurationMinutes,
            item.Calories,
            item.Allergens,
            item.Volume,
            smallPrice: null,
            smallSpecialOfferPrice: null,
            smallVolume: null,
            smallName: null,
            mediumPrice: null,
            mediumSpecialOfferPrice: null,
            mediumVolume: null,
            mediumName: null,
            largePrice: null,
            largeSpecialOfferPrice: null,
            largeVolume: null,
            largeName: null,
            item.Tags,
            item.Badge,
            item.Featured,
            item.Accent,
            item.ShortLabel,
            item.DisplayOrder,
            item.IsActive,
            price: 120_000);

        Assert.Equal(120_000, item.Price);
        Assert.Null(item.MediumPrice);
    }

    [Fact]
    public void Update_RemovingAllSizesAndBasePrice_SavesUnpricedAndNotOrderable()
    {
        var item = CreateItem(price: null, medium: 90_000);

        item.Update(
            item.CategoryId,
            item.PersianName,
            item.EnglishName,
            item.Description,
            item.ImageUrl,
            item.GalleryImageUrls,
            item.SpecialOfferPrice,
            item.TaxPercent,
            item.IsOrderable,
            item.PreparingDurationMinutes,
            item.Calories,
            item.Allergens,
            item.Volume,
            smallPrice: null,
            smallSpecialOfferPrice: null,
            smallVolume: null,
            smallName: null,
            mediumPrice: null,
            mediumSpecialOfferPrice: null,
            mediumVolume: null,
            mediumName: null,
            largePrice: null,
            largeSpecialOfferPrice: null,
            largeVolume: null,
            largeName: null,
            item.Tags,
            item.Badge,
            item.Featured,
            item.Accent,
            item.ShortLabel,
            item.DisplayOrder,
            item.IsActive);

        Assert.Equal(0, item.Price);
        Assert.False(item.IsOrderable);
    }
}
