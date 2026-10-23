using Sugarista.Domain.Entities;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.UnitTests;

public class OrderTests
{
    private static Order CreateOrder(decimal taxPercent = 0m, decimal unitPrice = 100_000m)
    {
        var order = new Order(
            "SG-TEST01",
            "مهمان آزمایشی",
            "09120000000",
            null,
            totalAmount: 1m,
            paymentAuthority: "authority",
            SplitBillMode.Single);

        order.AddLine(new OrderLine(
            order.Id,
            Guid.NewGuid(),
            "لاته",
            "Latte",
            null,
            null,
            2,
            unitPrice,
            [],
            taxPercent: taxPercent));

        return order;
    }

    [Fact]
    public void RecalculateTotal_ShouldIncludeLineTotalsTaxAndDeliveryFee()
    {
        var order = CreateOrder(taxPercent: 10m, unitPrice: 100_000m);

        order.ApplyDeliveryMethod(Guid.NewGuid(), 30_000m);

        Assert.Equal(200_000m, order.Lines.Single().LineTotal);
        Assert.Equal(20_000m, order.TaxTotal);
        Assert.Equal(250_000m, order.TotalAmount);
    }

    [Fact]
    public void MarkPaid_ShouldSetAmountPaidAndRegisterFulfillment()
    {
        var order = CreateOrder();

        order.RecalculateTotal();
        order.MarkPaid();

        Assert.Equal(OrderStatus.Paid, order.Status);
        Assert.Equal(order.TotalAmount, order.AmountPaid);
        Assert.Equal(FulfillmentStatus.Registered, order.FulfillmentStatus);
        Assert.NotNull(order.PaidAt);
    }

    [Fact]
    public void RecordPartialPayment_ShouldMoveThroughPartiallyPaidToPaid()
    {
        var order = CreateOrder();
        order.RecalculateTotal();

        order.RecordPartialPayment(50_000m);

        Assert.Equal(OrderStatus.PartiallyPaid, order.Status);
        Assert.Equal(50_000m, order.AmountPaid);

        order.RecordPartialPayment(150_000m);

        Assert.Equal(OrderStatus.Paid, order.Status);
        Assert.Equal(order.TotalAmount, order.AmountPaid);
    }

    [Fact]
    public void RecordPartialPayment_ShouldRejectNonPositiveAmount()
    {
        var order = CreateOrder();
        order.RecalculateTotal();

        Assert.Throws<DomainException>(() => order.RecordPartialPayment(0m));
    }

    [Fact]
    public void UpdateFulfillment_ShouldRejectUnpaidOrders()
    {
        var order = CreateOrder();

        Assert.Throws<DomainException>(() => order.UpdateFulfillment(FulfillmentStatus.Preparing));
    }
}
