using Sugarista.Application.Orders.Commands.CreateOrderCheckout;

namespace Sugarista.Application.UnitTests;

public class CreateOrderCheckoutValidatorTests
{
    private static CheckoutLineRequest Line(string? payerKey = null) =>
        new(Guid.NewGuid(), null, 1, Array.Empty<Guid>(), payerKey, payerKey);

    [Fact]
    public void Validate_ShouldAcceptSimpleOrder()
    {
        var command = new CreateOrderCheckoutCommand(
            "مهمان",
            "09120000000",
            null,
            [Line()]);

        var result = new CreateOrderCheckoutCommandValidator().Validate(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_ShouldRejectByItemSplitWithoutPayer()
    {
        var command = new CreateOrderCheckoutCommand(
            "مهمان",
            "09120000000",
            null,
            [Line()],
            IsSplitBill: true,
            SplitBillMode: "ByItem",
            GuestCount: 2);

        var result = new CreateOrderCheckoutCommandValidator().Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName.Contains("PayerKey", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_ShouldRejectPercentageSharesThatDoNotSumTo100()
    {
        var command = new CreateOrderCheckoutCommand(
            "مهمان",
            "09120000000",
            null,
            [Line("guest-1")],
            IsSplitBill: true,
            SplitBillMode: "ByPercentage",
            GuestCount: 2,
            SplitShares:
            [
                new CheckoutSplitShareRequest("guest-1", "مهمان ۱", SharePercentage: 30m),
                new CheckoutSplitShareRequest("guest-2", "مهمان ۲", SharePercentage: 30m),
            ]);

        var result = new CreateOrderCheckoutCommandValidator().Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.ErrorMessage.Contains("sum to 100", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_ShouldRequireCustomerPhone()
    {
        var command = new CreateOrderCheckoutCommand(
            "مهمان",
            string.Empty,
            null,
            [Line()]);

        var result = new CreateOrderCheckoutCommandValidator().Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(command.CustomerPhone));
    }
}
