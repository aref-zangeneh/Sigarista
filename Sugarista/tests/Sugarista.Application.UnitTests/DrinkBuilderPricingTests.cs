using Sugarista.Application.DrinkBuilder;
using Sugarista.Domain.Entities;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Application.UnitTests;

public class DrinkBuilderPricingTests
{
    private const decimal BasePrice = 120_000m;

    [Fact]
    public void Resolve_AddsSliderAmountPrice()
    {
        var (profile, chocolate, _) = CreateSliderProfile();

        var snapshot = DrinkBuilderPricing.Resolve(
            profile,
            BasePrice,
            [new DrinkBuilderSelectionRequest(chocolate.Id, 45m)]);

        Assert.Equal(4_500m, snapshot.ComponentsTotal);
        Assert.Equal(124_500m, snapshot.UnitPrice);
        Assert.Single(snapshot.Components);
        Assert.Contains("شکلات 45 گرم", snapshot.SummaryLabels);
    }

    [Fact]
    public void Resolve_ExcludesZeroAmountEvenWhenMinimumIsPositive()
    {
        var (profile, chocolate, _) = CreateSliderProfile();

        var snapshot = DrinkBuilderPricing.Resolve(
            profile,
            BasePrice,
            [new DrinkBuilderSelectionRequest(chocolate.Id, 0m)]);

        Assert.Empty(snapshot.Components);
        Assert.Equal(0m, snapshot.ComponentsTotal);
        Assert.Equal(BasePrice, snapshot.UnitPrice);
    }

    [Fact]
    public void Resolve_UsesDefaultAmountWhenNoSelectionIsSent()
    {
        var (profile, chocolate, _) = CreateSliderProfile();
        chocolate.Update(
            chocolate.Name,
            chocolate.EnglishName,
            chocolate.Unit,
            chocolate.PricePerUnit,
            chocolate.MinAmount,
            chocolate.MaxAmount,
            chocolate.Step,
            defaultAmount: 20m,
            isDefault: true,
            isActive: true,
            displayOrder: chocolate.DisplayOrder);

        var snapshot = DrinkBuilderPricing.Resolve(profile, BasePrice, []);

        Assert.Equal(2_000m, snapshot.ComponentsTotal);
        Assert.Equal(122_000m, snapshot.UnitPrice);
    }

    [Fact]
    public void Resolve_AddsMultipleComponents()
    {
        var (profile, chocolate, caramel) = CreateSliderProfile();

        var snapshot = DrinkBuilderPricing.Resolve(
            profile,
            BasePrice,
            [
                new DrinkBuilderSelectionRequest(chocolate.Id, 30m),
                new DrinkBuilderSelectionRequest(caramel.Id, 15m),
            ]);

        Assert.Equal(3_000m + 1_200m, snapshot.ComponentsTotal);
        Assert.Equal(124_200m, snapshot.UnitPrice);
    }

    [Fact]
    public void Resolve_ThrowsWhenAmountBelowMinimum()
    {
        var (profile, chocolate, _) = CreateSliderProfile();

        Assert.Throws<DomainException>(() => DrinkBuilderPricing.Resolve(
            profile,
            BasePrice,
            [new DrinkBuilderSelectionRequest(chocolate.Id, 5m)]));
    }

    [Fact]
    public void Resolve_ThrowsWhenAmountAboveMaximum()
    {
        var (profile, chocolate, _) = CreateSliderProfile();

        Assert.Throws<DomainException>(() => DrinkBuilderPricing.Resolve(
            profile,
            BasePrice,
            [new DrinkBuilderSelectionRequest(chocolate.Id, 65m)]));
    }

    [Fact]
    public void Resolve_ThrowsWhenAmountIsNotAligned()
    {
        var (profile, chocolate, _) = CreateSliderProfile();

        Assert.Throws<DomainException>(() => DrinkBuilderPricing.Resolve(
            profile,
            BasePrice,
            [new DrinkBuilderSelectionRequest(chocolate.Id, 12m)]));
    }

    [Fact]
    public void Resolve_ThrowsForNegativeAmount()
    {
        var (profile, chocolate, _) = CreateSliderProfile();

        Assert.Throws<DomainException>(() => DrinkBuilderPricing.Resolve(
            profile,
            BasePrice,
            [new DrinkBuilderSelectionRequest(chocolate.Id, -5m)]));
    }

    [Fact]
    public void Resolve_ThrowsForUnknownOption()
    {
        var (profile, _, _) = CreateSliderProfile();

        Assert.Throws<DomainException>(() => DrinkBuilderPricing.Resolve(
            profile,
            BasePrice,
            [new DrinkBuilderSelectionRequest(Guid.NewGuid(), 10m)]));
    }

    [Fact]
    public void Resolve_ThrowsForDuplicateOption()
    {
        var (profile, chocolate, _) = CreateSliderProfile();

        Assert.Throws<DomainException>(() => DrinkBuilderPricing.Resolve(
            profile,
            BasePrice,
            [
                new DrinkBuilderSelectionRequest(chocolate.Id, 10m),
                new DrinkBuilderSelectionRequest(chocolate.Id, 20m),
            ]));
    }

    [Fact]
    public void Resolve_ThrowsForInactiveOption()
    {
        var (profile, chocolate, _) = CreateSliderProfile();
        chocolate.SetActive(false);

        Assert.Throws<DomainException>(() => DrinkBuilderPricing.Resolve(
            profile,
            BasePrice,
            [new DrinkBuilderSelectionRequest(chocolate.Id, 10m)]));
    }

    [Fact]
    public void Resolve_SingleChoiceGroup_RequiresSelection()
    {
        var profile = new DrinkBuilderProfile(Guid.NewGuid(), "لاته سفارشی");
        var milk = profile.AddGroup(
            "شیر",
            DrinkBuilderInputType.Choice,
            DrinkBuilderSelectionMode.Single,
            minSelections: 1,
            maxSelections: 1,
            isRequired: true,
            displayOrder: 1);
        milk.AddOption("شیر کامل", null, DrinkBuilderUnit.Milliliter, 0m, 0m, 1m, 1m, 0m, false, true, 1);

        Assert.Throws<DomainException>(() => DrinkBuilderPricing.Resolve(profile, BasePrice, []));
    }

    [Fact]
    public void Resolve_ChoiceOption_AddsFlatPrice()
    {
        var profile = new DrinkBuilderProfile(Guid.NewGuid(), "لاته سفارشی");
        var milk = profile.AddGroup(
            "شیر",
            DrinkBuilderInputType.Choice,
            DrinkBuilderSelectionMode.Single,
            minSelections: 1,
            maxSelections: 1,
            isRequired: true,
            displayOrder: 1);
        var oat = milk.AddOption("شیر جو", null, DrinkBuilderUnit.Milliliter, 15_000m, 0m, 1m, 1m, 0m, false, true, 1);

        var snapshot = DrinkBuilderPricing.Resolve(
            profile,
            BasePrice,
            [new DrinkBuilderSelectionRequest(oat.Id, 1m)]);

        Assert.Equal(15_000m, snapshot.ComponentsTotal);
        Assert.Equal(135_000m, snapshot.UnitPrice);
        Assert.Contains("شیر جو", snapshot.SummaryLabels);
    }

    [Fact]
    public void Resolve_ChoiceOption_RejectsNonBinaryAmount()
    {
        var profile = new DrinkBuilderProfile(Guid.NewGuid(), "لاته سفارشی");
        var milk = profile.AddGroup(
            "شیر",
            DrinkBuilderInputType.Choice,
            DrinkBuilderSelectionMode.Single,
            minSelections: 0,
            maxSelections: 1,
            isRequired: false,
            displayOrder: 1);
        var oat = milk.AddOption("شیر جو", null, DrinkBuilderUnit.Milliliter, 15_000m, 0m, 1m, 1m, 0m, false, true, 1);

        Assert.Throws<DomainException>(() => DrinkBuilderPricing.Resolve(
            profile,
            BasePrice,
            [new DrinkBuilderSelectionRequest(oat.Id, 0.5m)]));
    }

    [Fact]
    public void Resolve_MultipleChoiceGroup_RejectsTooManySelections()
    {
        var profile = new DrinkBuilderProfile(Guid.NewGuid(), "لاته سفارشی");
        var syrups = profile.AddGroup(
            "سیروپ",
            DrinkBuilderInputType.Choice,
            DrinkBuilderSelectionMode.Multiple,
            minSelections: 0,
            maxSelections: 2,
            isRequired: false,
            displayOrder: 1);
        var vanilla = syrups.AddOption("وانیل", null, DrinkBuilderUnit.Pump, 5_000m, 0m, 1m, 1m, 0m, false, true, 1);
        var caramel = syrups.AddOption("کارامل", null, DrinkBuilderUnit.Pump, 5_000m, 0m, 1m, 1m, 0m, false, true, 2);
        var hazelnut = syrups.AddOption("فندق", null, DrinkBuilderUnit.Pump, 5_000m, 0m, 1m, 1m, 0m, false, true, 3);

        Assert.Throws<DomainException>(() => DrinkBuilderPricing.Resolve(
            profile,
            BasePrice,
            [
                new DrinkBuilderSelectionRequest(vanilla.Id, 1m),
                new DrinkBuilderSelectionRequest(caramel.Id, 1m),
                new DrinkBuilderSelectionRequest(hazelnut.Id, 1m),
            ]));
    }

    [Fact]
    public void Resolve_ExplicitChoiceSelection_SuppressesOtherDefaults()
    {
        var profile = new DrinkBuilderProfile(Guid.NewGuid(), "لاته سفارشی");
        var milk = profile.AddGroup(
            "شیر",
            DrinkBuilderInputType.Choice,
            DrinkBuilderSelectionMode.Single,
            minSelections: 1,
            maxSelections: 1,
            isRequired: true,
            displayOrder: 1);
        milk.AddOption("شیر کامل", null, DrinkBuilderUnit.Milliliter, 0m, 0m, 1m, 1m, 1m, true, true, 1);
        var oat = milk.AddOption("شیر جو", null, DrinkBuilderUnit.Milliliter, 15_000m, 0m, 1m, 1m, 0m, false, true, 2);

        // The customer picked oat milk. The whole-milk default must not be
        // injected on top, otherwise a single-choice group ends up with two.
        var snapshot = DrinkBuilderPricing.Resolve(
            profile,
            BasePrice,
            [new DrinkBuilderSelectionRequest(oat.Id, 1m)]);

        Assert.Single(snapshot.Components);
        Assert.Equal(15_000m, snapshot.ComponentsTotal);
        Assert.Contains("شیر جو", snapshot.SummaryLabels);
        Assert.DoesNotContain("شیر کامل", snapshot.SummaryLabels);
    }

    [Fact]
    public void Resolve_ExplicitZeroAmount_SuppressesSliderDefault()
    {
        var (profile, chocolate, _) = CreateSliderProfile();
        chocolate.Update(
            chocolate.Name,
            chocolate.EnglishName,
            chocolate.Unit,
            chocolate.PricePerUnit,
            chocolate.MinAmount,
            chocolate.MaxAmount,
            chocolate.Step,
            defaultAmount: 20m,
            isDefault: true,
            isActive: true,
            displayOrder: chocolate.DisplayOrder);

        var snapshot = DrinkBuilderPricing.Resolve(
            profile,
            BasePrice,
            [new DrinkBuilderSelectionRequest(chocolate.Id, 0m)]);

        Assert.Empty(snapshot.Components);
        Assert.Equal(BasePrice, snapshot.UnitPrice);
    }

    [Fact]
    public void ResolveEditedUnitPrice_PreservesComponentTotal()
    {
        var (profile, chocolate, _) = CreateSliderProfile();
        var snapshot = DrinkBuilderPricing.Resolve(
            profile,
            BasePrice,
            [new DrinkBuilderSelectionRequest(chocolate.Id, 45m)]);
        var json = DrinkBuilderPricing.Serialize(snapshot);

        Assert.Equal(124_500m, DrinkBuilderPricing.ResolveEditedUnitPrice(BasePrice, json));

        // If the admin changed the base menu price, the stored component total
        // still applies on top of the new base price.
        Assert.Equal(134_500m, DrinkBuilderPricing.ResolveEditedUnitPrice(130_000m, json));
    }

    [Fact]
    public void ResolveEditedUnitPrice_ReturnsBasePriceWithoutCustomization()
    {
        Assert.Equal(BasePrice, DrinkBuilderPricing.ResolveEditedUnitPrice(BasePrice, null));
        Assert.Equal(BasePrice, DrinkBuilderPricing.ResolveEditedUnitPrice(BasePrice, "bad-json"));
    }

    [Fact]
    public void Serialize_RoundTripsSnapshot()
    {
        var (profile, chocolate, _) = CreateSliderProfile();
        var snapshot = DrinkBuilderPricing.Resolve(
            profile,
            BasePrice,
            [new DrinkBuilderSelectionRequest(chocolate.Id, 45m)]);

        var json = DrinkBuilderPricing.Serialize(snapshot);
        var restored = DrinkBuilderPricing.Deserialize(json);

        Assert.NotNull(restored);
        Assert.Equal(snapshot.ProfileId, restored!.ProfileId);
        Assert.Equal(snapshot.UnitPrice, restored.UnitPrice);
        Assert.Equal(snapshot.ComponentsTotal, restored.ComponentsTotal);
        Assert.Single(restored.Components);
    }

    [Fact]
    public void Deserialize_ReturnsNullForInvalidJson()
    {
        Assert.Null(DrinkBuilderPricing.Deserialize("not-json"));
        Assert.Null(DrinkBuilderPricing.Deserialize(null));
        Assert.Null(DrinkBuilderPricing.Deserialize("  "));
    }

    [Theory]
    [InlineData(DrinkBuilderUnit.Gram, "گرم")]
    [InlineData(DrinkBuilderUnit.Milliliter, "میلی‌لیتر")]
    [InlineData(DrinkBuilderUnit.Piece, "عدد")]
    [InlineData(DrinkBuilderUnit.Shot, "شات")]
    [InlineData(DrinkBuilderUnit.Pump, "پمپ")]
    public void UnitLabel_ReturnsPersianLabel(DrinkBuilderUnit unit, string expected)
    {
        Assert.Equal(expected, DrinkBuilderPricing.UnitLabel(unit));
    }

    private static (DrinkBuilderProfile Profile, DrinkBuilderOption Chocolate, DrinkBuilderOption Caramel)
        CreateSliderProfile()
    {
        var profile = new DrinkBuilderProfile(Guid.NewGuid(), "لاته سفارشی");
        var group = profile.AddGroup(
            "افزودنی‌ها",
            DrinkBuilderInputType.Slider,
            DrinkBuilderSelectionMode.Multiple,
            minSelections: 0,
            maxSelections: 5,
            isRequired: false,
            displayOrder: 1);

        var chocolate = group.AddOption(
            "شکلات",
            "Chocolate",
            DrinkBuilderUnit.Gram,
            pricePerUnit: 100m,
            minAmount: 10m,
            maxAmount: 60m,
            step: 5m,
            defaultAmount: 0m,
            isDefault: false,
            isActive: true,
            displayOrder: 1);

        var caramel = group.AddOption(
            "کارامل",
            "Caramel",
            DrinkBuilderUnit.Gram,
            pricePerUnit: 80m,
            minAmount: 0m,
            maxAmount: 40m,
            step: 5m,
            defaultAmount: 0m,
            isDefault: false,
            isActive: true,
            displayOrder: 2);

        return (profile, chocolate, caramel);
    }
}
