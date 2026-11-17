using Sugarista.Domain.Entities;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.UnitTests.MenuItemTests;

/// <summary>
/// Recipe contents carry a customer-visibility flag: ingredient names marked
/// "show to customer" appear in the public digital menu; the quantity and the
/// preparation note never leave the staff side.
/// </summary>
public class RecipeItemShowToCustomerTests
{
    private static RecipeItem Create(
        decimal quantity = 45,
        string? preparationNote = "دم کن",
        bool showToCustomer = true)
    {
        return new RecipeItem(
            Guid.NewGuid(),
            Guid.NewGuid(),
            quantity,
            preparationNote,
            showToCustomer);
    }

    [Fact]
    public void ShowToCustomer_DefaultsToTrue()
    {
        var recipeItem = new RecipeItem(Guid.NewGuid(), Guid.NewGuid(), 30);

        Assert.True(recipeItem.ShowToCustomer);
    }

    [Fact]
    public void ShowToCustomer_CanBeTurnedOff()
    {
        var recipeItem = Create(showToCustomer: false);

        Assert.False(recipeItem.ShowToCustomer);
    }

    [Fact]
    public void Update_PreservesVisibilityToggle()
    {
        var recipeItem = Create(showToCustomer: true);

        recipeItem.Update(recipeItem.MenuItemId, recipeItem.IngredientId, 60, "دم کن", showToCustomer: false);

        Assert.False(recipeItem.ShowToCustomer);
        Assert.Equal(60, recipeItem.Quantity);
        Assert.Equal("دم کن", recipeItem.PreparationNote);
    }

    [Fact]
    public void ZeroQuantity_StillThrows()
    {
        Assert.Throws<DomainException>(() => Create(quantity: 0));
    }
}
