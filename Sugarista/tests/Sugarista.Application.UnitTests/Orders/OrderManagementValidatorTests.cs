using Sugarista.Application.Orders.Commands.DeleteAdminOrder;
using Sugarista.Application.Orders.Commands.RegisterAdminPayment;
using Sugarista.Application.Orders.Commands.SetOrderAccountingExclusion;
using Sugarista.Application.Pos.Commands.UpsertPosOrder;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.UnitTests.Orders;

public class OrderManagementValidatorTests
{
    [Theory]
    [InlineData("Hold", true)]
    [InlineData("PayCash", true)]
    [InlineData("PayCard", true)]
    [InlineData("InternalUse", true)]
    [InlineData("Complimentary", true)]
    [InlineData("UnknownAction", false)]
    public void UpsertPosOrderValidator_ShouldValidatePosActionsIncludingNonRevenue(string action, bool expectedValid)
    {
        var validator = new UpsertPosOrderCommandValidator();
        var command = new UpsertPosOrderCommand(
            OrderId: null,
            CustomerName: "پرسنل",
            CustomerPhone: null,
            Note: null,
            ServiceType: "DineIn",
            SalonTableId: null,
            Items: [new PosOrderLineRequest(Guid.NewGuid(), null, 1, [])],
            Action: action,
            OrderKind: action is "InternalUse" or "Complimentary" ? action : "Standard",
            ComplimentaryReason: action is "InternalUse" or "Complimentary" ? "شیفت صبح" : null);

        var result = validator.Validate(command);

        Assert.Equal(expectedValid, result.IsValid);
    }

    [Fact]
    public void UpsertPosOrderValidator_ShouldRejectInvalidOrderKindAndOversizedReason()
    {
        var validator = new UpsertPosOrderCommandValidator();
        var command = new UpsertPosOrderCommand(
            OrderId: null,
            CustomerName: null,
            CustomerPhone: null,
            Note: null,
            ServiceType: "TakeAway",
            SalonTableId: null,
            Items: [new PosOrderLineRequest(Guid.NewGuid(), null, 1, [])],
            Action: "Complimentary",
            OrderKind: "BogusKind",
            ComplimentaryReason: new string('a', 301));

        var result = validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpsertPosOrderCommand.OrderKind));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpsertPosOrderCommand.ComplimentaryReason));
    }

    [Fact]
    public void DeleteAdminOrderValidator_ShouldRequireNonEmptyOrderId()
    {
        var validator = new DeleteAdminOrderCommandValidator();

        Assert.False(validator.Validate(new DeleteAdminOrderCommand(Guid.Empty)).IsValid);
        Assert.True(validator.Validate(new DeleteAdminOrderCommand(Guid.NewGuid(), RestoreInventory: true)).IsValid);
        Assert.True(validator.Validate(new DeleteAdminOrderCommand(Guid.NewGuid(), RestoreInventory: false)).IsValid);
    }

    [Fact]
    public void SetOrderAccountingExclusionValidator_ShouldValidateOrderIdAndReasonLength()
    {
        var validator = new SetOrderAccountingExclusionCommandValidator();

        Assert.False(validator.Validate(new SetOrderAccountingExclusionCommand(Guid.Empty, true)).IsValid);
        Assert.False(validator.Validate(new SetOrderAccountingExclusionCommand(Guid.NewGuid(), true, new string('x', 301))).IsValid);
        Assert.True(validator.Validate(new SetOrderAccountingExclusionCommand(Guid.NewGuid(), true, "سفارش تستی")).IsValid);
        Assert.True(validator.Validate(new SetOrderAccountingExclusionCommand(Guid.NewGuid(), false, null)).IsValid);
    }

    [Theory]
    [InlineData("Cash", "Standard", true)]
    [InlineData("Complimentary", "InternalUse", true)]
    [InlineData("Complimentary", "Complimentary", true)]
    [InlineData("Cash", "InvalidKind", false)]
    public void RegisterAdminPaymentValidator_ShouldValidateOrderKindAndMethod(
        string paymentMethod,
        string orderKind,
        bool expectedValid)
    {
        var validator = new RegisterAdminPaymentCommandValidator();
        var command = new RegisterAdminPaymentCommand(Guid.NewGuid(), paymentMethod, null, orderKind, "توضیح");

        var result = validator.Validate(command);

        Assert.Equal(expectedValid, result.IsValid);
    }
}
