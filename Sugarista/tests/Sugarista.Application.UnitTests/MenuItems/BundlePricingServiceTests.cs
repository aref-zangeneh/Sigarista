using System.Reflection;
using Sugarista.Application.MenuItems.Bundles;
using Sugarista.Application.MenuItems.Bundles.Commands.ReplaceMenuItemBundle;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.UnitTests.MenuItems;

public class BundlePricingServiceTests
{
    private static MenuItem CreateItem(
        string persianName,
        string englishName,
        decimal price,
        decimal? smallPrice = null,
        decimal? mediumPrice = null,
        decimal? largePrice = null,
        decimal? specialOfferPrice = null,
        bool isActive = true,
        bool isOrderable = true)
    {
        return new MenuItem(
            categoryId: Guid.NewGuid(),
            persianName: persianName,
            englishName: englishName,
            description: string.Empty,
            imageUrl: null,
            galleryImageUrls: [],
            specialOfferPrice: specialOfferPrice,
            taxPercent: 0m,
            isOrderable: isOrderable,
            preparingDurationMinutes: 5,
            calories: 200,
            allergens: [],
            volume: null,
            smallPrice: smallPrice,
            smallSpecialOfferPrice: null,
            smallVolume: null,
            smallName: null,
            mediumPrice: mediumPrice,
            mediumSpecialOfferPrice: null,
            mediumVolume: null,
            mediumName: null,
            largePrice: largePrice,
            largeSpecialOfferPrice: null,
            largeVolume: null,
            largeName: null,
            tags: [],
            badge: null,
            featured: false,
            accent: "#5b3425",
            shortLabel: "ب",
            displayOrder: 0,
            isActive: isActive,
            price: price);
    }

    private static MenuItemBundleItem CreateBundleLine(MenuItem bundle, MenuItem component, int qty = 1, string? sizeKey = null, int order = 0)
    {
        var line = new MenuItemBundleItem(bundle.Id, component.Id, qty, sizeKey, order);
        typeof(MenuItemBundleItem)
            .GetProperty(nameof(MenuItemBundleItem.ComponentMenuItem), BindingFlags.Public | BindingFlags.Instance)!
            .SetValue(line, component);
        return line;
    }

    [Fact]
    public void Evaluate_Americano200kPlusSanSebastian300kAt430k_ComputesExactRealTotalAndSavings()
    {
        var americano = CreateItem("آمریکانو", "Americano", 200_000m);
        var cheesecake = CreateItem("چیزکیک سن‌سباستین", "San Sebastian Cheesecake", 300_000m);
        var bundle = CreateItem("باندل کافه", "Cafe Bundle", 430_000m);
        bundle.SetIsBundle(true);

        var lines = new[]
        {
            CreateBundleLine(bundle, americano, qty: 1, order: 0),
            CreateBundleLine(bundle, cheesecake, qty: 1, order: 1),
        };

        var valuation = BundlePricingService.Evaluate(bundle, lines);

        Assert.Equal(500_000m, valuation.RegularTotal);
        Assert.Equal(430_000m, valuation.BundlePrice);
        Assert.Equal(70_000m, valuation.SavingsAmount);
        Assert.Equal(14, valuation.SavingsPercent);
        Assert.True(valuation.AllComponentsAvailable);
        Assert.Equal(2, valuation.Components.Count);
        Assert.Equal(200_000m, valuation.Components[0].UnitRegularPrice);
        Assert.Equal(300_000m, valuation.Components[1].UnitRegularPrice);
    }

    [Fact]
    public void Evaluate_ComponentPriceUpdates_ReflectsLiveStandalonePriceAutomatically()
    {
        var americano = CreateItem("آمریکانو", "Americano", 230_000m);
        var cheesecake = CreateItem("چیزکیک سن‌سباستین", "San Sebastian", 300_000m);
        var bundle = CreateItem("باندل کافه", "Cafe Bundle", 430_000m);
        bundle.SetIsBundle(true);

        var valuation = BundlePricingService.Evaluate(bundle, new[]
        {
            CreateBundleLine(bundle, americano, qty: 1, order: 0),
            CreateBundleLine(bundle, cheesecake, qty: 1, order: 1),
        });

        Assert.Equal(530_000m, valuation.RegularTotal);
        Assert.Equal(100_000m, valuation.SavingsAmount);
        Assert.Equal(19, valuation.SavingsPercent);
    }

    [Fact]
    public void Evaluate_SizeVariantsAndMultipleQuantity_ResolvesExactVariantPrice()
    {
        var latte = CreateItem("لاته", "Latte", 0m, smallPrice: 180_000m, mediumPrice: 210_000m, largePrice: 250_000m);
        var cookie = CreateItem("کوکی", "Cookie", 90_000m);
        var bundle = CreateItem("باندل دو نفره", "Duo Bundle", 500_000m);
        bundle.SetIsBundle(true);

        var valuation = BundlePricingService.Evaluate(bundle, new[]
        {
            CreateBundleLine(bundle, latte, qty: 2, sizeKey: "large", order: 0),
            CreateBundleLine(bundle, cookie, qty: 1, order: 1),
        });

        // 2 * 250,000 + 1 * 90,000 = 590,000 -> bundle 500,000 -> savings 90,000 (15%)
        Assert.Equal(590_000m, valuation.RegularTotal);
        Assert.Equal(90_000m, valuation.SavingsAmount);
        Assert.Equal(15, valuation.SavingsPercent);
        Assert.Equal("large", valuation.Components[0].SizeKey);
        Assert.Equal("بزرگ", valuation.Components[0].SizeLabel);
        Assert.Equal(500_000m, valuation.Components[0].LineRegularPrice);
    }

    [Fact]
    public void Evaluate_BundlePricedAboveRegularTotal_ClampsSavingsToZero()
    {
        var espresso = CreateItem("اسپرسو", "Espresso", 150_000m);
        var bundle = CreateItem("باندل", "Bundle", 200_000m);
        bundle.SetIsBundle(true);

        var valuation = BundlePricingService.Evaluate(bundle, new[]
        {
            CreateBundleLine(bundle, espresso, qty: 1),
        });

        Assert.Equal(150_000m, valuation.RegularTotal);
        Assert.Equal(0m, valuation.SavingsAmount);
        Assert.Equal(0, valuation.SavingsPercent);
    }

    [Fact]
    public void Evaluate_InactiveOrUnorderableComponent_MarksBundleUnavailable()
    {
        var americano = CreateItem("آمریکانو", "Americano", 200_000m, isOrderable: true);
        var cheesecake = CreateItem("چیزکیک", "Cheesecake", 300_000m, isOrderable: false);
        var bundle = CreateItem("باندل", "Bundle", 430_000m);
        bundle.SetIsBundle(true);

        var valuation = BundlePricingService.Evaluate(bundle, new[]
        {
            CreateBundleLine(bundle, americano, qty: 1),
            CreateBundleLine(bundle, cheesecake, qty: 1),
        });

        Assert.False(valuation.AllComponentsAvailable);
        Assert.True(valuation.Components[0].IsAvailable);
        Assert.False(valuation.Components[1].IsAvailable);
    }

    [Fact]
    public void Evaluate_ComputesCogsAndProfitMarginFromComponentAndOwnRecipes()
    {
        var coffeeBeans = new Ingredient("دانه قهوه", "Coffee Beans", "gr", "", 0, unitCost: 2_000m);
        var creamCheese = new Ingredient("پنیر خامه‌ای", "Cream Cheese", "gr", "", 1, unitCost: 1_500m);
        var giftBox = new Ingredient("جعبه باندل", "Bundle Box", "pieces", "", 2, unitCost: 15_000m, kind: "packaging");

        var americano = CreateItem("آمریکانو", "Americano", 200_000m);
        americano.RecipeItems.Add(AttachIngredient(new RecipeItem(americano.Id, coffeeBeans.Id, 20m, null), coffeeBeans)); // 40,000

        var cheesecake = CreateItem("چیزکیک", "Cheesecake", 300_000m);
        cheesecake.RecipeItems.Add(AttachIngredient(new RecipeItem(cheesecake.Id, creamCheese.Id, 60m, null), creamCheese)); // 90,000

        var bundle = CreateItem("باندل کافه", "Cafe Bundle", 430_000m);
        bundle.SetIsBundle(true);
        bundle.RecipeItems.Add(AttachIngredient(new RecipeItem(bundle.Id, giftBox.Id, 1m, null), giftBox)); // 15,000

        var valuation = BundlePricingService.Evaluate(bundle, new[]
        {
            CreateBundleLine(bundle, americano, qty: 1),
            CreateBundleLine(bundle, cheesecake, qty: 1),
        });

        // Total COGS = 40,000 + 90,000 + 15,000 = 145,000
        // Profit = 430,000 - 145,000 = 285,000
        Assert.Equal(145_000m, valuation.TotalCostPerServing);
        Assert.Equal(285_000m, valuation.EstimatedProfit);
        Assert.Equal(66.3m, valuation.EstimatedMarginPercent);
    }

    [Fact]
    public void SerializeAndDeserializeSnapshot_RoundTripsAccurately()
    {
        var americano = CreateItem("آمریکانو", "Americano", 200_000m);
        var cheesecake = CreateItem("چیزکیک سن‌سباستین", "San Sebastian", 300_000m);
        var bundle = CreateItem("باندل کافه", "Cafe Bundle", 430_000m);
        bundle.SetIsBundle(true);

        var valuation = BundlePricingService.Evaluate(bundle, new[]
        {
            CreateBundleLine(bundle, americano, qty: 1, order: 0),
            CreateBundleLine(bundle, cheesecake, qty: 1, order: 1),
        });

        var json = BundlePricingService.SerializeSnapshot(valuation);
        var roundTrip = BundlePricingService.DeserializeSnapshot(json);

        Assert.NotNull(roundTrip);
        Assert.Equal(500_000m, roundTrip!.RegularTotal);
        Assert.Equal(430_000m, roundTrip.BundleUnitPrice);
        Assert.Equal(70_000m, roundTrip.SavingsAmount);
        Assert.Equal(14, roundTrip.SavingsPercent);
        Assert.Equal(2, roundTrip.Components.Count);
        Assert.Equal("آمریکانو", roundTrip.Components[0].PersianName);
        Assert.Null(BundlePricingService.DeserializeSnapshot("not-json"));
    }

    [Fact]
    public void ReplaceMenuItemBundleCommandValidator_EnforcesComponentRulesWhenEnabled()
    {
        var validator = new ReplaceMenuItemBundleCommandValidator();

        var invalidBundle = new ReplaceMenuItemBundleCommand(Guid.NewGuid(), true, 430_000m, []);
        Assert.False(validator.Validate(invalidBundle).IsValid);

        var disablingBundle = new ReplaceMenuItemBundleCommand(Guid.NewGuid(), false, null, []);
        Assert.True(validator.Validate(disablingBundle).IsValid);

        var validBundle = new ReplaceMenuItemBundleCommand(
            Guid.NewGuid(),
            true,
            430_000m,
            [new UpsertBundleComponentInput(Guid.NewGuid(), 1, null, 0)]);
        Assert.True(validator.Validate(validBundle).IsValid);
    }

    private static RecipeItem AttachIngredient(RecipeItem line, Ingredient ingredient)
    {
        typeof(RecipeItem)
            .GetProperty(nameof(RecipeItem.Ingredient), BindingFlags.Public | BindingFlags.Instance)!
            .SetValue(line, ingredient);
        return line;
    }
}
