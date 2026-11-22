using Sugarista.Domain.Entities;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.UnitTests;

public class WarehouseVoucherTests
{
    [Fact]
    public void Constructor_ShouldValidateReasonMatchesVoucherType()
    {
        var receipt = new WarehouseVoucher(
            "GRN-20261122-0001",
            WarehouseVoucherType.Receipt,
            WarehouseVoucherReason.Purchase,
            warehouseId: null,
            counterpartyName: "پخش قهوه مرکزی",
            externalReference: "INV-9001",
            createdByName: "انباردار ارشد");

        Assert.Equal(WarehouseVoucherStatus.Posted, receipt.Status);
        Assert.Equal(WarehouseVoucherType.Receipt, receipt.VoucherType);
        Assert.Equal("GRN-20261122-0001", receipt.VoucherNumber);

        // Issue reason on a Receipt must be rejected
        Assert.Throws<DomainException>(() => new WarehouseVoucher(
            "GRN-20261122-0002",
            WarehouseVoucherType.Receipt,
            WarehouseVoucherReason.WastageOrSpoilage,
            warehouseId: null));

        // Receipt reason on an Issue must be rejected
        Assert.Throws<DomainException>(() => new WarehouseVoucher(
            "GIV-20261122-0001",
            WarehouseVoucherType.Issue,
            WarehouseVoucherReason.OpeningBalance,
            warehouseId: null));
    }

    [Fact]
    public void AddLine_ShouldComputeLineTotalAndAccumulateVoucherTotal()
    {
        var voucher = new WarehouseVoucher(
            "GRN-20261122-0003",
            WarehouseVoucherType.Receipt,
            WarehouseVoucherReason.Purchase,
            warehouseId: null);

        var line1 = voucher.AddLine(Guid.NewGuid(), 2.5m, 400_000m, 10m, 12.5m, "B-01", new DateOnly(2027, 1, 15));
        var line2 = voucher.AddLine(Guid.NewGuid(), 3m, 120_000m, 0m, 3m);

        Assert.Equal(1_000_000m, line1.LineTotal);
        Assert.Equal(360_000m, line2.LineTotal);
        Assert.Equal(1_360_000m, voucher.TotalAmount);
        Assert.Equal(2, voucher.Lines.Count);
    }

    [Fact]
    public void Cancel_ShouldMarkCancelledAndRejectDoubleCancellation()
    {
        var voucher = new WarehouseVoucher(
            "GIV-20261122-0001",
            WarehouseVoucherType.Issue,
            WarehouseVoucherReason.KitchenConsumption,
            warehouseId: null);

        Assert.Throws<DomainException>(() => voucher.Cancel("   "));

        voucher.Cancel("ثبت اشتباه مقدار", "مدیر انبار");

        Assert.Equal(WarehouseVoucherStatus.Cancelled, voucher.Status);
        Assert.Equal("ثبت اشتباه مقدار", voucher.CancellationReason);
        Assert.Equal("مدیر انبار", voucher.CancelledByName);
        Assert.NotNull(voucher.CancelledAt);

        Assert.Throws<DomainException>(() => voucher.Cancel("تلاش مجدد"));
    }

    [Fact]
    public void InventoryAndWarehouseStock_ReceiveIssueAndReverse_WorkAccurately()
    {
        var ingredientId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();

        var mainStock = new InventoryStock(ingredientId);
        var m1 = mainStock.ReceiveVoucher(25m, "رسید GRN-1");
        Assert.Equal(25m, mainStock.CurrentQuantity);
        Assert.Equal(StockMovementType.WarehouseReceipt, m1.MovementType);
        Assert.Equal(25m, m1.Quantity);

        var m2 = mainStock.IssueVoucher(10m, "خروج GIV-1");
        Assert.Equal(15m, mainStock.CurrentQuantity);
        Assert.Equal(StockMovementType.WarehouseIssue, m2.MovementType);
        Assert.Equal(-10m, m2.Quantity);

        // Cannot issue more than available
        Assert.Throws<DomainException>(() => mainStock.IssueVoucher(20m, "بیش از موجودی"));

        // Reverse the issue (+10)
        var m3 = mainStock.ReverseVoucher(10m, "ابطال خروج");
        Assert.Equal(25m, mainStock.CurrentQuantity);
        Assert.Equal(StockMovementType.VoucherReversal, m3.MovementType);

        // WarehouseStock works identically
        var subStock = new WarehouseStock(warehouseId, ingredientId);
        var w1 = subStock.ReceiveVoucher(50m, "رسید انبارک");
        Assert.Equal(50m, subStock.Quantity);
        Assert.Equal(warehouseId, w1.WarehouseId);

        var w2 = subStock.IssueVoucher(12.5m, "خروج انبارک");
        Assert.Equal(37.5m, subStock.Quantity);
        Assert.Equal(-12.5m, w2.Quantity);

        Assert.Throws<DomainException>(() => subStock.ReverseVoucher(-40m, "ابطال بدون موجودی کافی"));
    }
}
