using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

/// <summary>
/// An atomic inter-warehouse transfer slip (حواله انتقال بین انبارها).
/// Null <see cref="SourceWarehouseId"/> or <see cref="DestinationWarehouseId"/>
/// represents the main operational stock (<see cref="InventoryStock"/> — انبار اصلی / موجودی مرکزی).
/// </summary>
public class WarehouseTransfer : BaseAuditableEntity
{
    public const int MaxReferenceLength = 40;
    public const int MaxNoteLength = 500;
    public const int MaxTransferredByLength = 150;

    private WarehouseTransfer()
    {
    }

    public WarehouseTransfer(
        string referenceNumber,
        Guid? sourceWarehouseId,
        Guid? destinationWarehouseId,
        string? note = null,
        string? transferredBy = null,
        DateTimeOffset? transferredAt = null)
    {
        if (string.IsNullOrWhiteSpace(referenceNumber))
        {
            throw new DomainException("شماره حواله انتقال الزامی است.");
        }

        if (sourceWarehouseId == destinationWarehouseId)
        {
            throw new DomainException("انبار مبدا و مقصد نمی‌توانند یکسان باشند.");
        }

        if (sourceWarehouseId == Guid.Empty || destinationWarehouseId == Guid.Empty)
        {
            throw new DomainException("شناسه انبار مبدا یا مقصد نامعتبر است.");
        }

        ReferenceNumber = referenceNumber.Trim();
        if (ReferenceNumber.Length > MaxReferenceLength)
        {
            ReferenceNumber = ReferenceNumber[..MaxReferenceLength];
        }

        SourceWarehouseId = sourceWarehouseId;
        DestinationWarehouseId = destinationWarehouseId;

        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (Note is { Length: > MaxNoteLength })
        {
            Note = Note[..MaxNoteLength];
        }

        TransferredBy = string.IsNullOrWhiteSpace(transferredBy) ? null : transferredBy.Trim();
        if (TransferredBy is { Length: > MaxTransferredByLength })
        {
            TransferredBy = TransferredBy[..MaxTransferredByLength];
        }

        TransferredAt = transferredAt ?? DateTimeOffset.UtcNow;
    }

    public string ReferenceNumber { get; private set; } = string.Empty;

    /// <summary>Null = main inventory stock (انبار اصلی); set = admin-defined warehouse/sub-warehouse.</summary>
    public Guid? SourceWarehouseId { get; private set; }

    public Warehouse? SourceWarehouse { get; private set; }

    /// <summary>Null = main inventory stock (انبار اصلی); set = admin-defined warehouse/sub-warehouse.</summary>
    public Guid? DestinationWarehouseId { get; private set; }

    public Warehouse? DestinationWarehouse { get; private set; }

    public string? Note { get; private set; }

    public string? TransferredBy { get; private set; }

    public DateTimeOffset TransferredAt { get; private set; }

    public ICollection<WarehouseTransferLine> Lines { get; private set; } = [];

    public WarehouseTransferLine AddLine(
        Guid ingredientId,
        decimal quantity,
        decimal sourceBefore,
        decimal sourceAfter,
        decimal destinationBefore,
        decimal destinationAfter)
    {
        var line = new WarehouseTransferLine(
            Id,
            ingredientId,
            quantity,
            sourceBefore,
            sourceAfter,
            destinationBefore,
            destinationAfter);

        Lines.Add(line);
        return line;
    }
}

public class WarehouseTransferLine : BaseEntity
{
    private WarehouseTransferLine()
    {
    }

    public WarehouseTransferLine(
        Guid transferId,
        Guid ingredientId,
        decimal quantity,
        decimal sourceQuantityBefore,
        decimal sourceQuantityAfter,
        decimal destinationQuantityBefore,
        decimal destinationQuantityAfter)
    {
        if (transferId == Guid.Empty)
        {
            throw new DomainException("شناسه حواله انتقال الزامی است.");
        }

        if (ingredientId == Guid.Empty)
        {
            throw new DomainException("انتخاب ماده اولیه برای انتقال الزامی است.");
        }

        if (quantity <= 0)
        {
            throw new DomainException("مقدار انتقال باید بزرگ‌تر از صفر باشد.");
        }

        TransferId = transferId;
        IngredientId = ingredientId;
        Quantity = quantity;
        SourceQuantityBefore = sourceQuantityBefore;
        SourceQuantityAfter = sourceQuantityAfter;
        DestinationQuantityBefore = destinationQuantityBefore;
        DestinationQuantityAfter = destinationQuantityAfter;
    }

    public Guid TransferId { get; private set; }

    public WarehouseTransfer Transfer { get; private set; } = null!;

    public Guid IngredientId { get; private set; }

    public Ingredient Ingredient { get; private set; } = null!;

    public decimal Quantity { get; private set; }

    public decimal SourceQuantityBefore { get; private set; }

    public decimal SourceQuantityAfter { get; private set; }

    public decimal DestinationQuantityBefore { get; private set; }

    public decimal DestinationQuantityAfter { get; private set; }
}
