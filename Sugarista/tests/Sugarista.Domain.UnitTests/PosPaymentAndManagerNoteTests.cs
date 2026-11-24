using Sugarista.Domain.Constants;
using Sugarista.Domain.Entities;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.UnitTests;

public class PosPaymentAndManagerNoteTests
{
    private static Order CreatePosOrder(decimal unitPrice = 255_000m, int quantity = 1)
    {
        var order = Order.CreatePosDraft(
            trackingCode: "SG-POS01",
            customerName: "سارا رضایی",
            customerPhone: "09121112233",
            note: "کم شیرین",
            totalAmount: unitPrice * quantity,
            serviceType: PosServiceType.DineIn,
            salonTableId: null,
            tableSessionId: null,
            servedByAdminUserId: Guid.NewGuid());

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
    public void PayAtPos_WithCardToCard_MarksOrderPaidWithCardToCardMethod()
    {
        var order = CreatePosOrder(180_000m);

        order.PayAtPos(PosPaymentMethod.CardToCard);

        Assert.Equal(OrderStatus.Paid, order.Status);
        Assert.Equal(PosPaymentMethod.CardToCard, order.PaymentMethod);
        Assert.Equal(180_000m, order.AmountPaid);
        Assert.NotNull(order.PaidAt);
    }

    [Fact]
    public void RecordPosPayments_WithMixedCashAndCardToCard_SetsSplitMethodAndCreatesPaidLegs()
    {
        var order = CreatePosOrder(255_000m);

        var created = order.RecordPosPayments([
            (PosPaymentMethod.Cash, 100_000m, null),
            (PosPaymentMethod.CardToCard, 155_000m, "TRK-9988")
        ]);

        Assert.Equal(OrderStatus.Paid, order.Status);
        Assert.Equal(PosPaymentMethod.Split, order.PaymentMethod);
        Assert.Equal(255_000m, order.AmountPaid);
        Assert.Equal(2, created.Count);
        Assert.Equal(2, order.Payments.Count);

        var cashLeg = Assert.Single(created, p => p.PaymentMethod == PosPaymentMethod.Cash);
        Assert.Equal(100_000m, cashLeg.Amount);
        Assert.Equal(OrderPaymentStatus.Paid, cashLeg.Status);
        Assert.Equal("نقدی", cashLeg.PayerLabel);

        var c2cLeg = Assert.Single(created, p => p.PaymentMethod == PosPaymentMethod.CardToCard);
        Assert.Equal(155_000m, c2cLeg.Amount);
        Assert.Equal(OrderPaymentStatus.Paid, c2cLeg.Status);
        Assert.Equal("کارت به کارت", c2cLeg.PayerLabel);
        Assert.Equal("TRK-9988", c2cLeg.ReferenceId);
    }

    [Fact]
    public void RecordPosPayments_WithSingleCardToCardLeg_SetsCardToCardMethodInsteadOfSplit()
    {
        var order = CreatePosOrder(200_000m);

        var created = order.RecordPosPayments([
            (PosPaymentMethod.CardToCard, 200_000m, "4455")
        ]);

        Assert.Equal(OrderStatus.Paid, order.Status);
        Assert.Equal(PosPaymentMethod.CardToCard, order.PaymentMethod);
        Assert.Single(created);
    }

    [Fact]
    public void RecordPosPayments_WhenSumDoesNotMatchOrderTotal_ThrowsDomainException()
    {
        var order = CreatePosOrder(255_000m);

        var ex = Assert.Throws<DomainException>(() =>
            order.RecordPosPayments([
                (PosPaymentMethod.Cash, 100_000m, null),
                (PosPaymentMethod.Card, 100_000m, null)
            ]));

        Assert.Contains("مجموع مبالغ پرداختی", ex.Message);
        Assert.Equal(OrderStatus.Held, order.Status);
    }

    [Fact]
    public void SetManagerNote_TrimsClearsValidatesLengthAndSurvivesPayment()
    {
        var order = CreatePosOrder(150_000m);

        order.SetManagerNote("  یادداشت محرمانه ویژه مدیریت  ");
        Assert.Equal("یادداشت محرمانه ویژه مدیریت", order.ManagerNote);

        order.RecalculateTotal();
        order.PayAtPos(PosPaymentMethod.Cash);
        Assert.Equal("یادداشت محرمانه ویژه مدیریت", order.ManagerNote);

        order.SetManagerNote("   ");
        Assert.Null(order.ManagerNote);

        var tooLong = new string('م', 1001);
        Assert.Throws<DomainException>(() => order.SetManagerNote(tooLong));
    }

    [Fact]
    public void PosManagerNotePermission_IsGrantedToAdminAndExcludedFromCashier()
    {
        Assert.True(AdminPermissions.IsValid(AdminPermissions.Pos.ManagerNote));
        Assert.Contains(AdminPermissions.Catalog, p => p.Key == AdminPermissions.Pos.ManagerNote && p.SectionId == "pos");
        Assert.Contains(AdminPermissions.Pos.ManagerNote, DefaultRolePermissions.Admin);
        Assert.DoesNotContain(AdminPermissions.Pos.ManagerNote, DefaultRolePermissions.Cashier);
        Assert.DoesNotContain(AdminPermissions.Pos.ManagerNote, DefaultRolePermissions.InventoryManager);
        Assert.DoesNotContain(AdminPermissions.Pos.ManagerNote, DefaultRolePermissions.Hr);
    }
}
