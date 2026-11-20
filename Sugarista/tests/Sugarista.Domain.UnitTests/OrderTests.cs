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

    [Fact]
    public void CompleteWithoutCharge_InternalUse_ShouldZeroTotalMarkPaidAndRegisterFulfillment()
    {
        var order = CreateOrder(taxPercent: 10m, unitPrice: 120_000m);
        order.RecalculateTotal();

        order.CompleteWithoutCharge(OrderKind.InternalUse, "شیفت صبح پرسنل");

        Assert.Equal(OrderKind.InternalUse, order.OrderKind);
        Assert.True(order.IsNonRevenue);
        Assert.Equal("شیفت صبح پرسنل", order.ComplimentaryReason);
        Assert.Equal(PosPaymentMethod.Complimentary, order.PaymentMethod);
        Assert.Equal(OrderStatus.Paid, order.Status);
        Assert.Equal(0m, order.TotalAmount);
        Assert.Equal(0m, order.AmountPaid);
        Assert.Equal(0m, order.TaxTotal);
        Assert.Equal(FulfillmentStatus.Registered, order.FulfillmentStatus);
        Assert.NotNull(order.PaidAt);
        // Line catalog prices stay preserved for COGS / audit reporting
        Assert.Equal(240_000m, order.Lines.Single().LineTotal);
    }

    [Fact]
    public void CompleteWithoutCharge_Complimentary_FromHeldPosDraft_ShouldSucceed()
    {
        var order = Order.CreatePosDraft(
            "SG-COMP01",
            "آقای رضایی",
            null,
            null,
            150_000m,
            PosServiceType.DineIn,
            null,
            null,
            null);
        order.AddLine(new OrderLine(order.Id, Guid.NewGuid(), "اسپرسو", "Espresso", null, null, 1, 150_000m, []));

        order.CompleteWithoutCharge(OrderKind.Complimentary, "مهمان مدیریت");

        Assert.Equal(OrderKind.Complimentary, order.OrderKind);
        Assert.Equal(OrderStatus.Paid, order.Status);
        Assert.Null(order.HeldAt);
        Assert.Equal(0m, order.TotalAmount);
        Assert.Equal("مهمان مدیریت", order.ComplimentaryReason);
    }

    [Fact]
    public void CompleteWithoutCharge_ShouldRejectStandardKindOrAlreadyPaidOrder()
    {
        var order = CreateOrder();

        Assert.Throws<DomainException>(() => order.CompleteWithoutCharge(OrderKind.Standard));

        order.RecalculateTotal();
        order.MarkPaid();

        Assert.Throws<DomainException>(() => order.CompleteWithoutCharge(OrderKind.Complimentary));
    }

    [Fact]
    public void PayAtPos_ShouldRejectComplimentaryPaymentMethod()
    {
        var order = CreateOrder();
        Assert.Throws<DomainException>(() => order.PayAtPos(PosPaymentMethod.Complimentary));
    }

    [Fact]
    public void RecalculateTotal_WhenNonRevenue_ShouldStayZero()
    {
        var order = CreateOrder(taxPercent: 9m, unitPrice: 100_000m);
        order.ConfigureOrderKind(OrderKind.InternalUse, "تست کیفیت");

        order.RecalculateTotal();

        Assert.Equal(0m, order.TotalAmount);
        Assert.Equal(0m, order.TaxTotal);
        Assert.Equal(0m, order.DiscountTotal);
    }

    [Fact]
    public void ExcludeAndIncludeInAccounting_ShouldToggleStateAndReason()
    {
        var order = CreateOrder();

        Assert.False(order.IsExcludedFromAccounting);
        Assert.Null(order.ExcludedFromAccountingAt);

        order.ExcludeFromAccounting("سفارش تستی");

        Assert.True(order.IsExcludedFromAccounting);
        Assert.NotNull(order.ExcludedFromAccountingAt);
        Assert.Equal("سفارش تستی", order.ExcludedFromAccountingReason);

        order.IncludeInAccounting();

        Assert.False(order.IsExcludedFromAccounting);
        Assert.Null(order.ExcludedFromAccountingAt);
        Assert.Null(order.ExcludedFromAccountingReason);
    }

    [Fact]
    public void AccountingAccountCodes_ShouldSeedInternalAndComplimentaryAccounts()
    {
        Assert.Contains(
            Sugarista.Domain.Constants.AccountingAccountCodes.SeedAccounts,
            a => a.Code == Sugarista.Domain.Constants.AccountingAccountCodes.InternalConsumptionExpense
                 && a.Type == AccountType.Expense);
        Assert.Contains(
            Sugarista.Domain.Constants.AccountingAccountCodes.SeedAccounts,
            a => a.Code == Sugarista.Domain.Constants.AccountingAccountCodes.ComplimentaryHospitalityExpense
                 && a.Type == AccountType.Expense);
    }
}
