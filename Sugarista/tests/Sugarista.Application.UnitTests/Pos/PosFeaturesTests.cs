using Sugarista.Application.Customers.Services;
using Sugarista.Application.Orders.Commands.UpdateAdminOrder;
using Sugarista.Application.Pos;
using Sugarista.Application.Pos.Commands.UpdatePosOrderManagerNote;
using Sugarista.Application.Pos.Commands.UpsertPosOrder;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.UnitTests.Pos;

public class PosFeaturesTests
{
    private readonly UpsertPosOrderCommandValidator _upsertValidator = new();

    private static UpsertPosOrderCommand BuildValidBaseCommand(string action = "PayCash") =>
        new(
            OrderId: null,
            CustomerName: "علی احمدی",
            CustomerPhone: "09123456789",
            Note: null,
            ServiceType: "DineIn",
            SalonTableId: null,
            Items: [new PosOrderLineRequest(Guid.NewGuid(), null, 1, [])],
            Action: action);

    [Fact]
    public void UpsertValidator_AcceptsPayCardToCardAndValidPaySplitAllocations()
    {
        var c2c = BuildValidBaseCommand("PayCardToCard") with
        {
            PaymentReference = "TRK-1020",
            ManagerNote = "تایید رسید کارت به کارت توسط مدیر"
        };
        Assert.True(_upsertValidator.Validate(c2c).IsValid);

        var split = BuildValidBaseCommand("PaySplit") with
        {
            PaymentAllocations =
            [
                new PosPaymentAllocationRequest("Cash", 80_000m),
                new PosPaymentAllocationRequest("Card", 100_000m),
                new PosPaymentAllocationRequest("CardToCard", 75_000m, "4412")
            ]
        };
        Assert.True(_upsertValidator.Validate(split).IsValid);
    }

    [Fact]
    public void UpsertValidator_RejectsEmptyOrInvalidPaySplitAllocations()
    {
        var emptySplit = BuildValidBaseCommand("PaySplit") with { PaymentAllocations = [] };
        var res1 = _upsertValidator.Validate(emptySplit);
        Assert.False(res1.IsValid);

        var badMethodSplit = BuildValidBaseCommand("PaySplit") with
        {
            PaymentAllocations = [new PosPaymentAllocationRequest("Complimentary", 100_000m)]
        };
        var res2 = _upsertValidator.Validate(badMethodSplit);
        Assert.False(res2.IsValid);

        var zeroAmountSplit = BuildValidBaseCommand("PaySplit") with
        {
            PaymentAllocations = [new PosPaymentAllocationRequest("Cash", 0m)]
        };
        var res3 = _upsertValidator.Validate(zeroAmountSplit);
        Assert.False(res3.IsValid);
    }

    [Fact]
    public void ManagerNoteValidator_EnforcesMaxLength()
    {
        var validator = new UpdatePosOrderManagerNoteCommandValidator();
        Assert.True(validator.Validate(new UpdatePosOrderManagerNoteCommand(Guid.NewGuid(), "یادداشت کوتاه")).IsValid);
        Assert.False(validator.Validate(new UpdatePosOrderManagerNoteCommand(Guid.NewGuid(), new string('x', 1001))).IsValid);
    }

    [Theory]
    [InlineData("۰۹۱۲ ۱۲۳-۴۵۶۷", "09121234567")]
    [InlineData("+989121234567", "09121234567")]
    [InlineData("989121234567", "09121234567")]
    [InlineData("9121234567", "09121234567")]
    [InlineData("POS", "")]
    [InlineData("   ", "")]
    public void CustomerDirectoryService_NormalizePhone_CanonicalizesIranianMobilesAndIgnoresPosPlaceholder(
        string raw,
        string expected)
    {
        Assert.Equal(expected, CustomerDirectoryService.NormalizePhone(raw));
    }

    [Fact]
    public void CustomerDirectoryService_SplitPersonName_ExtractsFirstAndLastNameAndSkipsPlaceholders()
    {
        Assert.Equal(("سارا", "محمدی راد"), CustomerDirectoryService.SplitPersonName("سارا محمدی راد", "09121234567"));
        Assert.Equal(("آرش", null), CustomerDirectoryService.SplitPersonName("آرش", "09121234567"));
        Assert.Equal((null, null), CustomerDirectoryService.SplitPersonName("مهمان", "09121234567"));
        Assert.Equal((null, null), CustomerDirectoryService.SplitPersonName("09121234567", "09121234567"));
    }

    [Fact]
    public void PosAndAdminOrderMappers_RedactManagerNoteForNonManagersAndIncludePaymentLegs()
    {
        var order = Order.CreatePosDraft(
            trackingCode: "SG-RED01",
            customerName: "مهدی کریمی",
            customerPhone: "09127778899",
            note: "بدون شکر",
            totalAmount: 200_000m,
            serviceType: PosServiceType.TakeAway,
            salonTableId: null,
            tableSessionId: null,
            servedByAdminUserId: null);

        order.AddLine(new OrderLine(
            order.Id,
            Guid.NewGuid(),
            "اسپرسو",
            "Espresso",
            null,
            null,
            1,
            200_000m,
            []));
        order.RecalculateTotal();
        order.SetManagerNote("یادداشت محرمانه مدیر شیفت");
        order.RecordPosPayments([
            (PosPaymentMethod.Cash, 50_000m, null),
            (PosPaymentMethod.CardToCard, 150_000m, "7711")
        ]);

        // Non-manager (Cashier): ManagerNote MUST be null
        var cashierPosDetail = PosOrderMapper.ToDetail(order, includeManagerNote: false);
        var cashierPosSummary = PosOrderMapper.ToSummary(order, includeManagerNote: false);
        var cashierAdminDetail = AdminOrderMapper.ToDetail(order, includeManagerNote: false);

        Assert.Null(cashierPosDetail.ManagerNote);
        Assert.Null(cashierPosSummary.ManagerNote);
        Assert.Null(cashierAdminDetail.ManagerNote);
        Assert.Equal(2, cashierPosDetail.PaymentLegs!.Count);

        // Manager: ManagerNote IS visible
        var managerPosDetail = PosOrderMapper.ToDetail(order, includeManagerNote: true);
        var managerPosSummary = PosOrderMapper.ToSummary(order, includeManagerNote: true);
        var managerAdminDetail = AdminOrderMapper.ToDetail(order, includeManagerNote: true);

        Assert.Equal("یادداشت محرمانه مدیر شیفت", managerPosDetail.ManagerNote);
        Assert.Equal("یادداشت محرمانه مدیر شیفت", managerPosSummary.ManagerNote);
        Assert.Equal("یادداشت محرمانه مدیر شیفت", managerAdminDetail.ManagerNote);
    }
}
