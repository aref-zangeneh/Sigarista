using Sugarista.Application.MenuItems.OptionGroups;
using Sugarista.Domain.Entities;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Application.UnitTests;

public class MenuItemOptionPricingTests
{
    [Fact]
    public void Resolve_AddsPriceDeltaOfSelectedOptions()
    {
        // Latte 200k: required "100% arabica" +120k, optional "30 ml hazelnut" +50k.
        var (beanGroup, syrupGroup, coffeeOption, syrupOption) = CreateExampleLatte();

        var snapshot = MenuItemOptionPricing.Resolve(
            [beanGroup, syrupGroup],
            [
                new MenuItemOptionSelectionRequest(coffeeOption.Id),
                new MenuItemOptionSelectionRequest(syrupOption.Id),
            ]);

        Assert.Equal(170_000m, snapshot.OptionsTotal);
        Assert.Equal(2, snapshot.Components.Count);
        Assert.Contains(coffeeOption.PersianName, snapshot.Labels);
        Assert.Contains(syrupOption.PersianName, snapshot.Labels);
    }

    [Fact]
    public void Resolve_RequiredGroupWithoutSelectionThrows()
    {
        var (beanGroup, _, _, _) = CreateExampleLatte();

        var exception = Assert.Throws<DomainException>(() =>
            MenuItemOptionPricing.Resolve([beanGroup], []));

        Assert.Contains("درصد دانه قهوه", exception.Message);
    }

    [Fact]
    public void Resolve_UnknownOptionIsRejected()
    {
        var (beanGroup, _, _, _) = CreateExampleLatte();

        Assert.Throws<DomainException>(() => MenuItemOptionPricing.Resolve(
            [beanGroup],
            [new MenuItemOptionSelectionRequest(Guid.NewGuid())]));
    }

    [Fact]
    public void Resolve_RequiredGroupSatisfiedByDefaultSelection()
    {
        var (beanGroup, _, _, _) = CreateExampleLatte();

        var snapshot = MenuItemOptionPricing.Resolve(
            [beanGroup],
            beanGroup.Options.Where(option => option.IsDefault)
                .Select(option => new MenuItemOptionSelectionRequest(option.Id))
                .ToList());

        Assert.Equal(80_000m, snapshot.OptionsTotal);
    }

    [Fact]
    public void Resolve_MultiGroupEnforcesMaxSelections()
    {
        var group = CreateMultiGroup();

        var threeIds = group.Options
            .Take(3)
            .Select(option => new MenuItemOptionSelectionRequest(option.Id))
            .ToList();

        Assert.Throws<DomainException>(() => MenuItemOptionPricing.Resolve([group], threeIds));
    }

    [Fact]
    public void Resolve_AggregatesDuplicateIngredientUsage()
    {
        var group = new MenuItemOptionGroup(
            Guid.NewGuid(),
            "شیر",
            null,
            isRequired: false,
            MenuItemOptionGroup.MultiMode,
            0,
            2,
            0);

        var ingredientId = Guid.NewGuid();

        var jedde = group.AddOption("شیر جدّه", null, 20_000m, false, true, 0);
        jedde.Ingredients.Add(new MenuItemOptionIngredient(jedde.Id, ingredientId, 30m));

        var soy = group.AddOption("شیر سویا", null, 25_000m, false, true, 1);
        soy.Ingredients.Add(new MenuItemOptionIngredient(soy.Id, ingredientId, 50m));

        var snapshot = MenuItemOptionPricing.Resolve(
            [group],
            [new(jedde.Id), new(soy.Id)]);

        var usage = Assert.Single(snapshot.Ingredients);
        Assert.Equal(ingredientId, usage.IngredientId);
        Assert.Equal(80m, usage.Quantity);
    }

    [Fact]
    public void Resolve_SerializeRoundTrip_PreservesSnapshot()
    {
        var (beanGroup, syrupGroup, coffeeOption, syrupOption) = CreateExampleLatte();

        var snapshot = MenuItemOptionPricing.Resolve(
            [beanGroup, syrupGroup],
            [new(coffeeOption.Id), new(syrupOption.Id)]);

        var json = MenuItemOptionPricing.Serialize(snapshot);
        var restored = MenuItemOptionPricing.Deserialize(json);

        Assert.NotNull(restored);
        Assert.Equal(snapshot.OptionsTotal, restored!.OptionsTotal);
        Assert.Equal(snapshot.Components.Count, restored.Components.Count);
        Assert.Equal(snapshot.Labels.Count, restored.Labels.Count);
        Assert.Equal(MenuItemOptionPricing.Serialize(restored), json);
    }

    [Fact]
    public void Deserialize_ReturnsNullForEmptyOrInvalidJson()
    {
        Assert.Null(MenuItemOptionPricing.Deserialize(null));
        Assert.Null(MenuItemOptionPricing.Deserialize(""));
        Assert.Null(MenuItemOptionPricing.Deserialize("not-json"));
    }

    private static (MenuItemOptionGroup BeanGroup, MenuItemOptionGroup SyrupGroup, MenuItemOption Coffee, MenuItemOption Syrup)
        CreateExampleLatte()
    {
        var beanGroup = new MenuItemOptionGroup(
            Guid.NewGuid(),
            "درصد دانه قهوه",
            "Bean ratio",
            isRequired: true,
            MenuItemOptionGroup.SingleMode,
            1,
            1,
            0);

        var arabica = beanGroup.AddOption("۱۰۰٪ عربیکا", null, 120_000m, isDefault: false, isActive: true, 0);
        beanGroup.AddOption("۷۰/۳۰ عربیکا", null, 80_000m, isDefault: true, isActive: true, 1);

        var syrupGroup = new MenuItemOptionGroup(
            Guid.NewGuid(),
            "سیروپ دلخواه",
            "Optional syrup",
            isRequired: false,
            MenuItemOptionGroup.MultiMode,
            0,
            1,
            10);

        var hazelnut = syrupGroup.AddOption("۳۰ میل سیروپ فندوق", null, 50_000m, false, true, 0);

        return (beanGroup, syrupGroup, arabica, hazelnut);
    }

    private static MenuItemOptionGroup CreateMultiGroup()
    {
        var group = new MenuItemOptionGroup(
            Guid.NewGuid(),
            "سیروپ",
            null,
            isRequired: false,
            MenuItemOptionGroup.MultiMode,
            0,
            2,
            0);

        group.AddOption("وانیل", null, 15_000m, false, true, 0);
        group.AddOption("کارامل", null, 15_000m, false, true, 1);
        group.AddOption("فندق", null, 15_000m, false, true, 2);

        return group;
    }
}
