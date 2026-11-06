using Sugarista.Application.DrinkBuilder.Commands.UpsertDrinkBuilderProfile;

namespace Sugarista.Application.UnitTests;

public class DrinkBuilderCommandValidatorTests
{
    private static readonly UpsertDrinkBuilderProfileCommandValidator Validator = new();

    [Fact]
    public void ValidCommand_Passes()
    {
        var result = Validator.Validate(BuildCommand());

        Assert.True(result.IsValid);
    }

    [Fact]
    public void MissingMenuItem_Fails()
    {
        var command = BuildCommand() with { MenuItemId = Guid.Empty };

        var result = Validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName.Contains("MenuItemId", StringComparison.Ordinal));
    }

    [Fact]
    public void EmptyGroups_Fail()
    {
        var command = BuildCommand() with { Groups = [] };

        var result = Validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName.Contains("Groups", StringComparison.Ordinal));
    }

    [Fact]
    public void GroupWithoutOptions_Fails()
    {
        var command = BuildCommand();
        command = command with
        {
            Groups = [command.Groups.First() with { Options = [] }],
        };

        var result = Validator.Validate(command);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void InvalidInputType_Fails()
    {
        var command = BuildCommand();
        command = command with
        {
            Groups = [command.Groups.First() with { InputType = "Unknown" }],
        };

        var result = Validator.Validate(command);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void InvalidSelectionMode_Fails()
    {
        var command = BuildCommand();
        command = command with
        {
            Groups = [command.Groups.First() with { SelectionMode = "Unknown" }],
        };

        var result = Validator.Validate(command);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void SliderWithZeroStep_Fails()
    {
        var command = BuildCommand();
        var group = command.Groups.First();
        command = command with
        {
            Groups =
            [
                group with
                {
                    Options = [group.Options.First() with { Step = 0m }],
                },
            ],
        };

        var result = Validator.Validate(command);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void SliderWithMisalignedDefault_Fails()
    {
        var command = BuildCommand();
        var group = command.Groups.First();
        command = command with
        {
            Groups =
            [
                group with
                {
                    Options = [group.Options.First() with { DefaultAmount = 12m }],
                },
            ],
        };

        var result = Validator.Validate(command);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void SliderWithDefaultOutsideRange_Fails()
    {
        var command = BuildCommand();
        var group = command.Groups.First();
        command = command with
        {
            Groups =
            [
                group with
                {
                    Options = [group.Options.First() with { DefaultAmount = 75m }],
                },
            ],
        };

        var result = Validator.Validate(command);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void ChoiceGroupWithZeroStep_StillPasses_BecauseItIsNormalised()
    {
        var command = BuildCommand();
        var choiceGroup = command.Groups.Last();
        command = command with
        {
            Groups =
            [
                command.Groups.First(),
                choiceGroup with
                {
                    Options = [choiceGroup.Options.First() with { Step = 0m }],
                },
            ],
        };

        var result = Validator.Validate(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void NegativePrice_Fails()
    {
        var command = BuildCommand();
        var group = command.Groups.First();
        command = command with
        {
            Groups =
            [
                group with
                {
                    Options = [group.Options.First() with { PricePerUnit = -1m }],
                },
            ],
        };

        var result = Validator.Validate(command);

        Assert.False(result.IsValid);
    }

    private static UpsertDrinkBuilderProfileCommand BuildCommand() =>
        new(
            Id: null,
            MenuItemId: Guid.NewGuid(),
            Name: "لاته سفارشی",
            IsEnabled: true,
            DisplayOrder: 1,
            Groups:
            [
                new DrinkBuilderGroupInput(
                    Id: null,
                    Name: "افزودنی‌ها",
                    InputType: "Slider",
                    SelectionMode: "Multiple",
                    MinSelections: 0,
                    MaxSelections: 5,
                    IsRequired: false,
                    DisplayOrder: 1,
                    Options:
                    [
                        new DrinkBuilderOptionInput(
                            Id: null,
                            Name: "شکلات",
                            EnglishName: "Chocolate",
                            Unit: "Gram",
                            PricePerUnit: 100m,
                            MinAmount: 10m,
                            MaxAmount: 60m,
                            Step: 5m,
                            DefaultAmount: 0m,
                            IsDefault: false,
                            IsActive: true,
                            DisplayOrder: 1),
                    ]),
                new DrinkBuilderGroupInput(
                    Id: null,
                    Name: "شیر",
                    InputType: "Choice",
                    SelectionMode: "Single",
                    MinSelections: 1,
                    MaxSelections: 1,
                    IsRequired: true,
                    DisplayOrder: 2,
                    Options:
                    [
                        new DrinkBuilderOptionInput(
                            Id: null,
                            Name: "شیر کامل",
                            EnglishName: "Whole Milk",
                            Unit: "Milliliter",
                            PricePerUnit: 0m,
                            MinAmount: 0m,
                            MaxAmount: 1m,
                            Step: 1m,
                            DefaultAmount: 1m,
                            IsDefault: true,
                            IsActive: true,
                            DisplayOrder: 1),
                    ]),
            ]);
}
