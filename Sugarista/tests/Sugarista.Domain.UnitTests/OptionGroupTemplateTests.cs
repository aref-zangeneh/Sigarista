using Sugarista.Domain.Entities;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.UnitTests;

public class OptionGroupTemplateTests
{
    [Fact]
    public void Constructor_NormalizesFieldsAndEnforcesRequiredMinimums()
    {
        var template = new OptionGroupTemplate(
            "  نوشیدنی‌های بر پایه شیر  ",
            "  نوع شیر  ",
            "  Milk Type  ",
            isRequired: true,
            selectionMode: "single",
            minSelections: 0,
            maxSelections: 3,
            displayOrder: 2);

        Assert.Equal("نوشیدنی‌های بر پایه شیر", template.CategoryLabel);
        Assert.Equal("نوع شیر", template.PersianName);
        Assert.Equal("Milk Type", template.EnglishName);
        Assert.True(template.IsRequired);
        Assert.Equal(MenuItemOptionGroup.SingleMode, template.SelectionMode);
        Assert.Equal(1, template.MinSelections);
        Assert.Equal(1, template.MaxSelections);
        Assert.Equal(2, template.DisplayOrder);
        Assert.True(template.IsActive);
    }

    [Fact]
    public void AddOption_AndIngredient_StoresValuesAndRejectsInvalidInputs()
    {
        var template = new OptionGroupTemplate(
            "سیروپ‌ها",
            "طعم‌دهنده",
            "Syrup",
            isRequired: false,
            selectionMode: "Multi",
            minSelections: 0,
            maxSelections: 3,
            displayOrder: 0);

        var option = template.AddOption("سیروپ کارامل", "Caramel Syrup", 35_000m, false, true, 0);
        var ingredientId = Guid.NewGuid();
        var usage = new OptionGroupTemplateItemIngredient(option.Id, ingredientId, 15m);
        option.Ingredients.Add(usage);

        Assert.Single(template.Options);
        Assert.Single(option.Ingredients);
        Assert.Equal(35_000m, option.PriceDelta);
        Assert.Equal(15m, usage.Quantity);

        Assert.Throws<DomainException>(() => template.AddOption("", null, 0, false, true, 1));
        Assert.Throws<DomainException>(() => template.AddOption("وانیل", null, -100, false, true, 1));
        Assert.Throws<DomainException>(() => new OptionGroupTemplateItemIngredient(option.Id, ingredientId, 0));
    }

    [Fact]
    public void MenuItemOptionGroup_PreservesOptionalSourceTemplateId()
    {
        var templateId = Guid.NewGuid();
        var group = new MenuItemOptionGroup(
            Guid.NewGuid(),
            "نوع شیر",
            "Milk Type",
            isRequired: true,
            selectionMode: "Single",
            minSelections: 1,
            maxSelections: 1,
            displayOrder: 0,
            isActive: true,
            sourceTemplateId: templateId);

        Assert.Equal(templateId, group.SourceTemplateId);

        var customGroup = new MenuItemOptionGroup(
            Guid.NewGuid(),
            "درجه برشتگی",
            null,
            isRequired: false,
            selectionMode: "Single",
            minSelections: 0,
            maxSelections: 1,
            displayOrder: 1);

        Assert.Null(customGroup.SourceTemplateId);
    }

    [Fact]
    public void UncheckingRequired_ResetsMinSelectionsAndStaysOptional()
    {
        var template = new OptionGroupTemplate(
            "شیر",
            "نوع شیر",
            "Milk Type",
            isRequired: true,
            selectionMode: "Single",
            minSelections: 1,
            maxSelections: 1,
            displayOrder: 0);

        Assert.True(template.IsRequired);
        Assert.Equal(1, template.MinSelections);

        template.Update("شیر", "نوع شیر", "Milk Type", isRequired: false, "Single", minSelections: 1, maxSelections: 1, displayOrder: 0);

        Assert.False(template.IsRequired);
        Assert.Equal(0, template.MinSelections);

        var itemGroup = new MenuItemOptionGroup(
            Guid.NewGuid(),
            "نوع شیر",
            "Milk Type",
            isRequired: true,
            selectionMode: "Single",
            minSelections: 1,
            maxSelections: 1,
            displayOrder: 0);

        itemGroup.Update("نوع شیر", "Milk Type", isRequired: false, "Single", minSelections: 1, maxSelections: 1, displayOrder: 0);

        Assert.False(itemGroup.IsRequired);
        Assert.Equal(0, itemGroup.MinSelections);
    }
}
