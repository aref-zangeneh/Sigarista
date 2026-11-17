using Sugarista.Application.Warehouses.Commands.CreateWarehouse;
using Sugarista.Application.Warehouses.Hierarchy;

namespace Sugarista.Application.UnitTests.Warehouses;

public class CreateWarehouseCommandValidatorTests
{
    private readonly CreateWarehouseCommandValidator _validator = new();

    [Fact]
    public void ValidCommand_Passes()
    {
        var result = _validator.Validate(new CreateWarehouseCommand("انبار شمال", "WH-2", "توضیح", null, 3, true));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyName_Fails(string? name)
    {
        var result = _validator.Validate(new CreateWarehouseCommand(name!));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void TooLongName_Fails()
    {
        var result = _validator.Validate(new CreateWarehouseCommand(new string('ا', 121)));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void NegativeDisplayOrder_Fails()
    {
        var result = _validator.Validate(new CreateWarehouseCommand("انبار", DisplayOrder: -1));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void LongCode_Fails()
    {
        var result = _validator.Validate(new CreateWarehouseCommand("انبار", Code: new string('C', 41)));

        Assert.False(result.IsValid);
    }
}

public class WarehouseHierarchyGuardTests
{
    private static readonly (Guid Id, Guid? ParentId)[] Warehouses =
    [
        (Guid.Parse("11111111-1111-1111-1111-111111111111"), null),      // انبار A (top-level)
        (Guid.Parse("22222222-2222-2222-2222-222222222222"), Guid.Parse("11111111-1111-1111-1111-111111111111")), // انبارک زیر A
        (Guid.Parse("33333333-3333-3333-3333-333333333333"), null),      // انبار B (top-level)
    ];

    [Fact]
    public void NullParent_IsAlwaysAllowed()
    {
        var warehouseId = Guid.NewGuid();

        Assert.True(WarehouseHierarchyGuard.CanAdopt(Warehouses, warehouseId, null));
    }

    [Fact]
    public void SelfParent_IsRejected()
    {
        var warehouseId = Warehouses[0].Id;

        Assert.False(WarehouseHierarchyGuard.CanAdopt(Warehouses, warehouseId, warehouseId));
    }

    [Fact]
    public void TopLevelWarehouse_CanAdopt()
    {
        var childId = Guid.NewGuid();

        Assert.True(WarehouseHierarchyGuard.CanAdopt(Warehouses, childId, Warehouses[0].Id));
        Assert.True(WarehouseHierarchyGuard.CanAdopt(Warehouses, childId, Warehouses[2].Id));
    }

    [Fact]
    public void SubWarehouse_CannotAdopt()
    {
        var childId = Guid.NewGuid();
        var subWarehouseId = Warehouses[1].Id;

        Assert.False(WarehouseHierarchyGuard.CanAdopt(Warehouses, childId, subWarehouseId));
    }
}
