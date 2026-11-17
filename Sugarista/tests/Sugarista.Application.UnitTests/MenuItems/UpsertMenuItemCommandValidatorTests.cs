using Sugarista.Application.MenuItems.Commands.UpsertMenuItem;

namespace Sugarista.Application.UnitTests.MenuItems;

public class UpsertMenuItemCommandValidatorTests
{
    private static UpsertMenuItemCommand ValidCommand(
        decimal? price = null,
        decimal? small = null,
        decimal? medium = null,
        decimal? large = null)
    {
        return new UpsertMenuItemCommand(
            Id: null,
            CategoryId: Guid.NewGuid(),
            PersianName: "اسپرسو",
            EnglishName: "Espresso",
            Description: "قهوهٔ خالص",
            ImageUrl: null,
            GalleryImageUrls: [],
            SpecialOfferPrice: null,
            TaxPercent: 0,
            IsOrderable: true,
            PreparingDurationMinutes: 3,
            Calories: 5,
            Allergens: [],
            Volume: "35–40 ml",
            SmallPrice: small,
            SmallSpecialOfferPrice: null,
            SmallVolume: null,
            SmallName: null,
            MediumPrice: medium,
            MediumSpecialOfferPrice: null,
            MediumVolume: null,
            MediumName: null,
            LargePrice: large,
            LargeSpecialOfferPrice: null,
            LargeVolume: null,
            LargeName: null,
            Tags: [],
            Badge: null,
            Featured: false,
            Accent: "linear-gradient(135deg, #5b3425, #8a5339)",
            ShortLabel: "اس",
            DisplayOrder: 1,
            IsActive: true,
            Price: price);
    }

    [Fact]
    public void VariantLessItem_WithBasePrice_Passes()
    {
        var result = new UpsertMenuItemCommandValidator().Validate(ValidCommand(price: 120_000));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void VariantLessItem_WithoutBasePrice_Passes_PriceIsOptional()
    {
        // An item can be drafted without any price; the domain then keeps
        // ordering off until a price exists.
        var result = new UpsertMenuItemCommandValidator().Validate(ValidCommand());

        Assert.True(result.IsValid);
    }

    [Fact]
    public void SizedItem_WithoutBasePrice_Passes()
    {
        var result = new UpsertMenuItemCommandValidator().Validate(ValidCommand(medium: 90_000));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void NegativeBasePrice_Fails()
    {
        var result = new UpsertMenuItemCommandValidator().Validate(ValidCommand(price: -1));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void MissingPersianName_Fails()
    {
        var command = ValidCommand(price: 120_000) with { PersianName = "  " };

        var result = new UpsertMenuItemCommandValidator().Validate(command);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void MissingCategory_Fails()
    {
        var command = ValidCommand(price: 120_000) with { CategoryId = Guid.Empty };

        var result = new UpsertMenuItemCommandValidator().Validate(command);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void NameOnly_Item_Passes_AllOtherFieldsOptional()
    {
        var command = ValidCommand() with
        {
            EnglishName = "",
            Description = "",
            Accent = "",
            ShortLabel = "",
        };

        var result = new UpsertMenuItemCommandValidator().Validate(command);

        Assert.True(result.IsValid);
    }
}
