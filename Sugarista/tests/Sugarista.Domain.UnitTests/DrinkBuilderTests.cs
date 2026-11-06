using Sugarista.Domain.Entities;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.UnitTests;

public class DrinkBuilderTests
{
    [Fact]
    public void Profile_RequiresMenuItem()
    {
        Assert.Throws<DomainException>(() =>
            new DrinkBuilderProfile(Guid.Empty, "لاته سفارشی"));
    }

    [Fact]
    public void Profile_RequiresName()
    {
        Assert.Throws<DomainException>(() =>
            new DrinkBuilderProfile(Guid.NewGuid(), "  "));
    }

    [Fact]
    public void Profile_RejectsNegativeDisplayOrder()
    {
        Assert.Throws<DomainException>(() =>
            new DrinkBuilderProfile(Guid.NewGuid(), "لاته سفارشی", true, -1));
    }

    [Fact]
    public void Profile_ChangeMenuItem_RejectsEmptyGuid()
    {
        var profile = new DrinkBuilderProfile(Guid.NewGuid(), "لاته سفارشی");

        Assert.Throws<DomainException>(() => profile.ChangeMenuItem(Guid.Empty));
    }

    [Fact]
    public void Group_SingleSelection_ForcesMaximumToOne()
    {
        var profile = new DrinkBuilderProfile(Guid.NewGuid(), "لاته سفارشی");

        var group = profile.AddGroup(
            "شیر",
            DrinkBuilderInputType.Choice,
            DrinkBuilderSelectionMode.Single,
            minSelections: 0,
            maxSelections: 5,
            isRequired: false,
            displayOrder: 1);

        Assert.Equal(1, group.MaxSelections);
    }

    [Fact]
    public void Group_RequiredSelection_BumpsMinimumToOne()
    {
        var profile = new DrinkBuilderProfile(Guid.NewGuid(), "لاته سفارشی");

        var group = profile.AddGroup(
            "شیر",
            DrinkBuilderInputType.Choice,
            DrinkBuilderSelectionMode.Single,
            minSelections: 0,
            maxSelections: 1,
            isRequired: true,
            displayOrder: 1);

        Assert.Equal(1, group.MinSelections);
        Assert.True(group.IsRequired);
    }

    [Fact]
    public void Group_RejectsMinimumGreaterThanMaximum()
    {
        var profile = new DrinkBuilderProfile(Guid.NewGuid(), "لاته سفارشی");

        Assert.Throws<DomainException>(() => profile.AddGroup(
            "سیروپ",
            DrinkBuilderInputType.Choice,
            DrinkBuilderSelectionMode.Multiple,
            minSelections: 3,
            maxSelections: 2,
            isRequired: false,
            displayOrder: 1));
    }

    [Fact]
    public void Group_RejectsNonPositiveMaximum()
    {
        var profile = new DrinkBuilderProfile(Guid.NewGuid(), "لاته سفارشی");

        Assert.Throws<DomainException>(() => profile.AddGroup(
            "سیروپ",
            DrinkBuilderInputType.Choice,
            DrinkBuilderSelectionMode.Multiple,
            minSelections: 0,
            maxSelections: 0,
            isRequired: false,
            displayOrder: 1));
    }

    [Fact]
    public void Option_AllowsZeroDefault_WhenMinimumIsPositive()
    {
        var group = CreateSliderGroup();

        var option = group.AddOption(
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

        Assert.Equal(0m, option.DefaultAmount);
    }

    [Fact]
    public void Option_RejectsNegativePrice()
    {
        var group = CreateSliderGroup();

        Assert.Throws<DomainException>(() => group.AddOption(
            "شکلات", null, DrinkBuilderUnit.Gram, -1m, 0m, 10m, 1m, 0m, false, true, 1));
    }

    [Fact]
    public void Option_RejectsMaximumBelowMinimum()
    {
        var group = CreateSliderGroup();

        Assert.Throws<DomainException>(() => group.AddOption(
            "شکلات", null, DrinkBuilderUnit.Gram, 100m, 30m, 20m, 1m, 0m, false, true, 1));
    }

    [Fact]
    public void Option_RejectsNonPositiveStep()
    {
        var group = CreateSliderGroup();

        Assert.Throws<DomainException>(() => group.AddOption(
            "شکلات", null, DrinkBuilderUnit.Gram, 100m, 0m, 10m, 0m, 0m, false, true, 1));
    }

    [Fact]
    public void Option_RejectsDefaultOutsideRange()
    {
        var group = CreateSliderGroup();

        Assert.Throws<DomainException>(() => group.AddOption(
            "شکلات", null, DrinkBuilderUnit.Gram, 100m, 10m, 20m, 1m, 25m, false, true, 1));
    }

    [Fact]
    public void Option_RejectsMisalignedDefault()
    {
        var group = CreateSliderGroup();

        Assert.Throws<DomainException>(() => group.AddOption(
            "شکلات", null, DrinkBuilderUnit.Gram, 100m, 10m, 60m, 5m, 12m, false, true, 1));
    }

    [Fact]
    public void Option_RejectsDefaultFlagWithoutAmount()
    {
        var group = CreateSliderGroup();

        Assert.Throws<DomainException>(() => group.AddOption(
            "شکلات", null, DrinkBuilderUnit.Gram, 100m, 0m, 60m, 5m, 0m, true, true, 1));
    }

    [Theory]
    [InlineData(10, 10, 5, true)]
    [InlineData(15, 10, 5, true)]
    [InlineData(12, 10, 5, false)]
    [InlineData(0, 10, 5, false)]
    public void Option_IsAligned_RespectsGrid(decimal amount, decimal min, decimal step, bool expected)
    {
        Assert.Equal(expected, DrinkBuilderOption.IsAligned(amount, min, step));
    }

    private static DrinkBuilderGroup CreateSliderGroup()
    {
        var profile = new DrinkBuilderProfile(Guid.NewGuid(), "لاته سفارشی");
        return profile.AddGroup(
            "افزودنی‌ها",
            DrinkBuilderInputType.Slider,
            DrinkBuilderSelectionMode.Multiple,
            minSelections: 0,
            maxSelections: 5,
            isRequired: false,
            displayOrder: 1);
    }
}
