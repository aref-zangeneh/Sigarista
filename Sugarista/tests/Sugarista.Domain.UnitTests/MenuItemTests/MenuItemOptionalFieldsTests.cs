using Sugarista.Domain.Entities;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.UnitTests.MenuItemTests;

/// <summary>
/// Menu items can be drafted with only a name (plus a category): every other
/// field is optional and either falls back to a sensible default or stays
/// empty. An item without any price must never stay orderable.
/// </summary>
public class MenuItemOptionalFieldsTests
{
    private static MenuItem CreateItem(
        string persianName = "لاته",
        string englishName = "Latte",
        string description = "اسپرسو و شیر",
        bool isOrderable = true,
        decimal? price = null,
        decimal? smallPrice = null,
        decimal? mediumPrice = null,
        decimal? largePrice = null)
    {
        return new MenuItem(
            Guid.NewGuid(),
            persianName,
            englishName,
            description,
            imageUrl: null,
            galleryImageUrls: [],
            specialOfferPrice: null,
            taxPercent: 0,
            isOrderable,
            preparingDurationMinutes: null,
            calories: null,
            allergens: [],
            volume: null,
            smallPrice,
            smallSpecialOfferPrice: null,
            smallVolume: null,
            smallName: null,
            mediumPrice,
            mediumSpecialOfferPrice: null,
            mediumVolume: null,
            mediumName: null,
            largePrice,
            largeSpecialOfferPrice: null,
            largeVolume: null,
            largeName: null,
            tags: [],
            badge: null,
            featured: false,
            accent: "",
            shortLabel: "",
            displayOrder: 0,
            price: price);
    }

    [Fact]
    public void NameOnly_CanBeCreated_WithoutAnyOtherField()
    {
        var item = CreateItem(
            englishName: "",
            description: "");

        Assert.Equal("لاته", item.PersianName);
        Assert.Equal(string.Empty, item.EnglishName);
        Assert.Equal(string.Empty, item.Description);
        Assert.Equal(0, item.Price);
    }

    [Fact]
    public void NameOnly_EmptyAccentAndShortLabel_FallBackToDefaults()
    {
        var item = CreateItem(englishName: "", description: "");

        Assert.Equal("linear-gradient(135deg, #5b3425, #8a5339)", item.Accent);
        Assert.Equal("لا", item.ShortLabel); // first two characters of the Persian name
    }

    [Fact]
    public void UnpricedItem_IsAutomaticallyNotOrderable()
    {
        var item = CreateItem(englishName: "", description: "", isOrderable: true);

        Assert.Equal(0, item.Price);
        Assert.False(item.IsOrderable);
    }

    [Fact]
    public void PricedItem_KeepsRequestedOrderableState()
    {
        var orderable = CreateItem(price: 120_000, isOrderable: true);
        var nonOrderable = CreateItem(price: 120_000, isOrderable: false);

        Assert.True(orderable.IsOrderable);
        Assert.False(nonOrderable.IsOrderable);
    }

    [Fact]
    public void SizedItem_WithoutBasePrice_IsOrderableWhenSizePriceExists()
    {
        var item = CreateItem(mediumPrice: 90_000);

        Assert.Equal(90_000, item.Price);
        Assert.True(item.IsOrderable);
    }

    [Fact]
    public void AddingAPriceLater_TurnsOrderingBackOn()
    {
        var item = CreateItem(englishName: "", description: "");
        Assert.False(item.IsOrderable);

        item.Update(
            item.CategoryId,
            item.PersianName,
            item.EnglishName,
            item.Description,
            item.ImageUrl,
            item.GalleryImageUrls,
            item.SpecialOfferPrice,
            item.TaxPercent,
            isOrderable: true,
            item.PreparingDurationMinutes,
            item.Calories,
            item.Allergens,
            item.Volume,
            item.SmallPrice,
            item.SmallSpecialOfferPrice,
            item.SmallVolume,
            item.SmallName,
            item.MediumPrice,
            item.MediumSpecialOfferPrice,
            item.MediumVolume,
            item.MediumName,
            item.LargePrice,
            item.LargeSpecialOfferPrice,
            item.LargeVolume,
            item.LargeName,
            item.Tags,
            item.Badge,
            item.Featured,
            item.Accent,
            item.ShortLabel,
            item.DisplayOrder,
            item.IsActive,
            price: 150_000);

        Assert.Equal(150_000, item.Price);
        Assert.True(item.IsOrderable);
    }

    [Fact]
    public void EmptyPersianName_StillThrows()
    {
        Assert.Throws<DomainException>(() => CreateItem(persianName: "   "));
    }

    [Fact]
    public void EnglishName_OverLimit_Throws()
    {
        Assert.Throws<DomainException>(() => CreateItem(englishName: new string('x', 201)));
    }

    [Fact]
    public void Description_OverLimit_Throws()
    {
        Assert.Throws<DomainException>(() => CreateItem(description: new string('x', 1001)));
    }
}
