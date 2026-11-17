using Sugarista.Application.Inventory.Commands.AdjustStock;
using Sugarista.Application.Inventory.Commands.RestockIngredient;
using Sugarista.Application.Inventory.Commands.SetAlertThreshold;
using Sugarista.Application.Warehouses.Commands.UpdateWarehouse;

namespace Sugarista.Application.UnitTests.Warehouses;

public class UpdateWarehouseCommandValidatorTests
{
    private readonly UpdateWarehouseCommandValidator _validator = new();

    [Fact]
    public void ValidCommand_Passes()
    {
        var result = _validator.Validate(new UpdateWarehouseCommand(Guid.NewGuid(), "انبار شمال", null, null, null, 1, false));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void EmptyId_Fails()
    {
        var result = _validator.Validate(new UpdateWarehouseCommand(Guid.Empty, "انبار"));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void EmptyName_Fails()
    {
        var result = _validator.Validate(new UpdateWarehouseCommand(Guid.NewGuid(), " "));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void NegativeDisplayOrder_Fails()
    {
        var result = _validator.Validate(new UpdateWarehouseCommand(Guid.NewGuid(), "انبار", DisplayOrder: -2));

        Assert.False(result.IsValid);
    }
}

public class InventoryWarehouseCommandValidatorTests
{
    [Fact]
    public void Restock_WithoutWarehouse_IsStillValid()
    {
        var validator = new RestockIngredientCommandValidator();

        Assert.True(validator.Validate(new RestockIngredientCommand(Guid.NewGuid(), 5, "شارژ")).IsValid);
    }

    [Fact]
    public void Restock_NonPositiveQuantity_Fails()
    {
        var validator = new RestockIngredientCommandValidator();

        Assert.False(validator.Validate(new RestockIngredientCommand(Guid.NewGuid(), 0, "شارژ")).IsValid);
    }

    [Fact]
    public void Adjust_ZeroDelta_Fails()
    {
        var validator = new AdjustStockCommandValidator();

        Assert.False(validator.Validate(new AdjustStockCommand(Guid.NewGuid(), 0, "تعدیل")).IsValid);
    }

    [Fact]
    public void Adjust_NonZeroDelta_Passes()
    {
        var validator = new AdjustStockCommandValidator();

        Assert.True(validator.Validate(new AdjustStockCommand(Guid.NewGuid(), -2.5m, "اتلاف")).IsValid);
    }

    [Fact]
    public void Threshold_Negative_Fails()
    {
        var validator = new SetAlertThresholdCommandValidator();

        Assert.False(validator.Validate(new SetAlertThresholdCommand(Guid.NewGuid(), -1)).IsValid);
    }

    [Fact]
    public void Threshold_ValidWithWarehouse_Passes()
    {
        var validator = new SetAlertThresholdCommandValidator();

        Assert.True(validator.Validate(new SetAlertThresholdCommand(Guid.NewGuid(), 4, Guid.NewGuid())).IsValid);
    }
}
