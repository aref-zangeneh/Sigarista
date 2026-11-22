using Sugarista.Domain.Entities;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.UnitTests;

public class WarehouseTransferTests
{
    [Fact]
    public void WarehouseTransfer_RejectsSameSourceAndDestination()
    {
        var whId = Guid.NewGuid();
        Assert.Throws<DomainException>(() => new WarehouseTransfer("TRF-001", whId, whId));
        Assert.Throws<DomainException>(() => new WarehouseTransfer("TRF-002", null, null));
    }

    [Fact]
    public void WarehouseTransfer_AddsLinesWithExactBeforeAndAfterSnapshots()
    {
        var src = Guid.NewGuid();
        var dst = Guid.NewGuid();
        var ing = Guid.NewGuid();

        var transfer = new WarehouseTransfer("TRF-20261122-ABC123", src, dst, "شارژ بار گرم", "admin");
        var line = transfer.AddLine(ing, 250m, 1000m, 750m, 100m, 350m);

        Assert.Single(transfer.Lines);
        Assert.Equal(250m, line.Quantity);
        Assert.Equal(1000m, line.SourceQuantityBefore);
        Assert.Equal(750m, line.SourceQuantityAfter);
        Assert.Equal(100m, line.DestinationQuantityBefore);
        Assert.Equal(350m, line.DestinationQuantityAfter);
    }

    [Fact]
    public void InventoryStock_And_WarehouseStock_TransferOutAndIn_EnforceSufficientStockAndEmitMovements()
    {
        var ingId = Guid.NewGuid();
        var whId = Guid.NewGuid();

        var main = new InventoryStock(ingId);
        main.Restock(500m, "شارژ اولیه");

        var wh = new WarehouseStock(whId, ingId);

        // Cannot transfer more than available
        Assert.Throws<DomainException>(() => main.TransferOut(600m, "تست"));
        Assert.Throws<DomainException>(() => wh.TransferOut(10m, "تست"));

        // Transfer 200 from Main -> Warehouse
        var outMove = main.TransferOut(200m, "انتقال به انبارک");
        var inMove = wh.TransferIn(200m, "انتقال از انبار اصلی");

        Assert.Equal(300m, main.CurrentQuantity);
        Assert.Equal(200m, wh.Quantity);
        Assert.Equal(StockMovementType.TransferOut, outMove.MovementType);
        Assert.Equal(-200m, outMove.Quantity);
        Assert.Null(outMove.WarehouseId);
        Assert.Equal(StockMovementType.TransferIn, inMove.MovementType);
        Assert.Equal(200m, inMove.Quantity);
        Assert.Equal(whId, inMove.WarehouseId);

        // Transfer 50 back from Warehouse -> Main
        var backOut = wh.TransferOut(50m, "برگشت به انبار اصلی");
        var backIn = main.TransferIn(50m, "ورود از انبارک");

        Assert.Equal(150m, wh.Quantity);
        Assert.Equal(350m, main.CurrentQuantity);
        Assert.Equal(StockMovementType.TransferOut, backOut.MovementType);
        Assert.Equal(-50m, backOut.Quantity);
        Assert.Equal(whId, backOut.WarehouseId);
        Assert.Equal(StockMovementType.TransferIn, backIn.MovementType);
        Assert.Equal(50m, backIn.Quantity);
        Assert.Null(backIn.WarehouseId);
    }
}
