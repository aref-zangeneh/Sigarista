using Sugarista.Application.MoqAlerts;
using Sugarista.Application.MoqAlerts.Commands.QuickRestockMoqItem;
using Sugarista.Application.MoqAlerts.Commands.UpdateMoqThreshold;

namespace Sugarista.Application.UnitTests.MoqAlerts;

public class MoqAlertEvaluatorAndValidatorTests
{
    private readonly UpdateMoqThresholdCommandValidator _thresholdValidator = new();
    private readonly QuickRestockMoqItemCommandValidator _restockValidator = new();

    [Theory]
    [InlineData(0, 10, MoqSeverity.OutOfStock)]
    [InlineData(-5, 10, MoqSeverity.OutOfStock)]
    [InlineData(2, 10, MoqSeverity.Critical)]
    [InlineData(5, 10, MoqSeverity.Critical)]
    [InlineData(5.1, 10, MoqSeverity.Warning)]
    [InlineData(10, 10, MoqSeverity.Warning)]
    [InlineData(10.1, 10, MoqSeverity.Healthy)]
    [InlineData(0, 0, MoqSeverity.Healthy)]
    public void Classify_ComputesExpectedSeverity(decimal current, decimal moq, MoqSeverity expected)
    {
        Assert.Equal(expected, MoqAlertEvaluator.Classify(current, moq));
    }

    [Theory]
    [InlineData(0, 10, 10)]
    [InlineData(3.5, 10, 6.5)]
    [InlineData(10, 10, 0)]
    [InlineData(15, 10, 0)]
    [InlineData(0, 0, 0)]
    public void ComputeDeficit_ReturnsNonNegativeShortfall(decimal current, decimal moq, decimal expected)
    {
        Assert.Equal(expected, MoqAlertEvaluator.ComputeDeficit(current, moq));
    }

    [Theory]
    [InlineData(0, 10, 0)]
    [InlineData(-2, 10, 0)]
    [InlineData(5, 10, 50)]
    [InlineData(10, 10, 100)]
    [InlineData(25, 10, 100)]
    [InlineData(5, 0, 100)]
    public void ComputeFillPercent_ClampsBetweenZeroAndHundred(decimal current, decimal moq, decimal expected)
    {
        Assert.Equal(expected, MoqAlertEvaluator.ComputeFillPercent(current, moq));
    }

    [Fact]
    public void BuildAlertKey_DistinguishesWarehousesAndSources()
    {
        var itemId = Guid.NewGuid();
        var wh1 = Guid.NewGuid();
        var wh2 = Guid.NewGuid();

        var mainKey = MoqAlertEvaluator.BuildAlertKey(MoqSourceType.Ingredient, itemId);
        var menuKey = MoqAlertEvaluator.BuildAlertKey(MoqSourceType.MenuItem, itemId);
        var wh1Key = MoqAlertEvaluator.BuildAlertKey(MoqSourceType.WarehouseIngredient, itemId, wh1);
        var wh2Key = MoqAlertEvaluator.BuildAlertKey(MoqSourceType.WarehouseIngredient, itemId, wh2);

        var distinct = new HashSet<string> { mainKey, menuKey, wh1Key, wh2Key };
        Assert.Equal(4, distinct.Count);
    }

    [Fact]
    public void UpdateMoqThresholdValidator_ValidatesAllSources()
    {
        Assert.True(_thresholdValidator.Validate(
            new UpdateMoqThresholdCommand(MoqSourceType.Ingredient, Guid.NewGuid(), null, 500m)).IsValid);
        Assert.True(_thresholdValidator.Validate(
            new UpdateMoqThresholdCommand(MoqSourceType.MenuItem, Guid.NewGuid(), null, 0m)).IsValid);
        Assert.True(_thresholdValidator.Validate(
            new UpdateMoqThresholdCommand(MoqSourceType.WarehouseIngredient, Guid.NewGuid(), Guid.NewGuid(), 20m)).IsValid);

        // Missing warehouseId for warehouse ingredient fails
        Assert.False(_thresholdValidator.Validate(
            new UpdateMoqThresholdCommand(MoqSourceType.WarehouseIngredient, Guid.NewGuid(), null, 20m)).IsValid);
        // Negative MOQ fails
        Assert.False(_thresholdValidator.Validate(
            new UpdateMoqThresholdCommand(MoqSourceType.Ingredient, Guid.NewGuid(), null, -1m)).IsValid);
        // Empty itemId fails
        Assert.False(_thresholdValidator.Validate(
            new UpdateMoqThresholdCommand(MoqSourceType.Ingredient, Guid.Empty, null, 10m)).IsValid);
    }

    [Fact]
    public void QuickRestockValidator_ValidatesQuantityAndWarehouseId()
    {
        Assert.True(_restockValidator.Validate(
            new QuickRestockMoqItemCommand(MoqSourceType.Ingredient, Guid.NewGuid(), null, 100m, "خرید اضطراری")).IsValid);
        Assert.False(_restockValidator.Validate(
            new QuickRestockMoqItemCommand(MoqSourceType.Ingredient, Guid.NewGuid(), null, 0m)).IsValid);
        Assert.False(_restockValidator.Validate(
            new QuickRestockMoqItemCommand(MoqSourceType.Ingredient, Guid.NewGuid(), null, -10m)).IsValid);
        Assert.False(_restockValidator.Validate(
            new QuickRestockMoqItemCommand(MoqSourceType.WarehouseIngredient, Guid.NewGuid(), null, 10m)).IsValid);
    }
}
