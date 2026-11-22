using Sugarista.Domain.Entities;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.UnitTests;

public class OrderManualDiscountTests
{
    private static Order CreateSamplePosOrder(decimal unitPrice = 200_000m, int quantity = 1)
    {
        var order = Order.CreatePosDraft(
            "SG-TEST01",
            "مشتری تستی",
            "09120000000",
            null,
            unitPrice * quantity,
            PosServiceType.DineIn,
            null,
            null,
            null);

        order.AddLine(new OrderLine(
            order.Id,
            Guid.NewGuid(),
            "لاته",
            "Latte",
            null,
            null,
            quantity,
            unitPrice,
            []));

        order.RecalculateTotal();
        return order;
    }

    [Fact]
    public void ApplyManualDiscount_FixedAmount_ReducesTotalAndRecordsMetadata()
    {
        var order = CreateSamplePosOrder(200_000m, 1);

        var applied = order.ApplyManualDiscount(
            ManualDiscountType.Amount,
            35_000m,
            "تخفیف مشتری وفادار",
            "عارف");

        Assert.Equal(35_000m, applied);
        Assert.Equal(165_000m, order.TotalAmount);
        Assert.Equal(35_000m, order.DiscountTotal);
        Assert.Equal(ManualDiscountType.Amount, order.ManualDiscountType);
        Assert.Equal(35_000m, order.ManualDiscountValue);
        Assert.Equal(35_000m, order.ManualDiscountAmount);
        Assert.Equal("تخفیف مشتری وفادار", order.ManualDiscountReason);
        Assert.Equal("عارف", order.ManualDiscountAppliedBy);
    }

    [Fact]
    public void ApplyManualDiscount_Percent_ComputesRoundedAmountAndReducesTotal()
    {
        var order = CreateSamplePosOrder(250_000m, 1);

        var applied = order.ApplyManualDiscount(
            ManualDiscountType.Percent,
            15m,
            "تخفیف مدیریت",
            "مدیر کافه");

        Assert.Equal(37_500m, applied);
        Assert.Equal(212_500m, order.TotalAmount);
        Assert.Equal(37_500m, order.DiscountTotal);
        Assert.Equal(ManualDiscountType.Percent, order.ManualDiscountType);
        Assert.Equal(15m, order.ManualDiscountValue);
        Assert.Equal(37_500m, order.ManualDiscountAmount);
    }

    [Fact]
    public void ApplyManualDiscount_RejectsOutOfRangeValues()
    {
        var order = CreateSamplePosOrder(100_000m, 1);

        Assert.Throws<DomainException>(() => order.ApplyManualDiscount(ManualDiscountType.Amount, 0m));
        Assert.Throws<DomainException>(() => order.ApplyManualDiscount(ManualDiscountType.Amount, 150_000m));
        Assert.Throws<DomainException>(() => order.ApplyManualDiscount(ManualDiscountType.Percent, 105m));
    }

    [Fact]
    public void RecalculateTotal_ClearsManualDiscountSoEditsNeverDoubleStack()
    {
        var order = CreateSamplePosOrder(200_000m, 1);
        order.ApplyManualDiscount(ManualDiscountType.Percent, 10m, "تست", "ادمین");
        Assert.Equal(180_000m, order.TotalAmount);

        order.RecalculateTotal();

        Assert.Equal(200_000m, order.TotalAmount);
        Assert.Equal(0m, order.DiscountTotal);
        Assert.Null(order.ManualDiscountType);
        Assert.Null(order.ManualDiscountValue);
        Assert.Equal(0m, order.ManualDiscountAmount);
        Assert.Null(order.ManualDiscountReason);
        Assert.Null(order.ManualDiscountAppliedBy);
    }

    [Fact]
    public void CompleteWithoutCharge_ClearsManualDiscountAndZeroesTotal()
    {
        var order = CreateSamplePosOrder(200_000m, 1);
        order.ApplyManualDiscount(ManualDiscountType.Amount, 50_000m, "تست", "ادمین");

        order.CompleteWithoutCharge(OrderKind.Complimentary, "مهمان");

        Assert.Equal(0m, order.TotalAmount);
        Assert.Equal(0m, order.DiscountTotal);
        Assert.Equal(0m, order.ManualDiscountAmount);
        Assert.Null(order.ManualDiscountType);
    }
}
