using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

public enum WarehouseVoucherType
{
    /// <summary>رسید ورود به انبار (Goods Receipt Note — GRN).</summary>
    Receipt = 1,

    /// <summary>سند / حواله خروج از انبار (Goods Issue Voucher — GIV).</summary>
    Issue = 2,
}

public enum WarehouseVoucherStatus
{
    /// <summary>ثبت و قطعی‌شده (موجودی اعمال شده است).</summary>
    Posted = 1,

    /// <summary>ابطال‌شده (موجودی به‌صورت خودکار برگشت خورده است).</summary>
    Cancelled = 2,
}

public enum WarehouseVoucherReason
{
    // ── Receipt reasons (رسید ورود) ─────────────────────────
    Purchase = 1,
    ProductionReturn = 2,
    OpeningBalance = 3,
    InventorySurplus = 4,
    SampleOrGift = 5,
    OtherReceipt = 6,

    // ── Issue reasons (سند خروج) ────────────────────────────
    KitchenConsumption = 10,
    ProductionIssue = 11,
    WastageOrSpoilage = 12,
    InternalConsumption = 13,
    ReturnToSupplier = 14,
    InventoryShortage = 15,
    SampleOrPromo = 16,
    OtherIssue = 17,
}

/// <summary>
/// A formal Goods Receipt Note (رسید ورود به انبار) or Goods Issue Voucher (سند خروج از انبار).
/// Null <see cref="WarehouseId"/> targets the main operational stock (<see cref="InventoryStock"/>);
/// non-null targets an admin-defined warehouse or sub-warehouse (<see cref="WarehouseStock"/>).
/// Vouchers are never hard-deleted — cancellation posts reversing stock movements and marks the
/// voucher as <see cref="WarehouseVoucherStatus.Cancelled"/> for full auditability.
/// </summary>
public class WarehouseVoucher : BaseAuditableEntity
{
    public const int MaxVoucherNumberLength = 40;
    public const int MaxCounterpartyLength = 200;
    public const int MaxExternalReferenceLength = 100;
    public const int MaxNoteLength = 1000;
    public const int MaxActorNameLength = 150;
    public const int MaxCancellationReasonLength = 500;
    public const int MaxBatchNumberLength = 80;
    public const int MaxLineNoteLength = 300;

    private WarehouseVoucher()
    {
    }

    public WarehouseVoucher(
        string voucherNumber,
        WarehouseVoucherType voucherType,
        WarehouseVoucherReason reason,
        Guid? warehouseId,
        Guid? supplierId = null,
        string? counterpartyName = null,
        string? externalReference = null,
        DateTimeOffset? voucherDate = null,
        string? note = null,
        string? createdByName = null)
    {
        if (string.IsNullOrWhiteSpace(voucherNumber))
        {
            throw new DomainException("شماره سند انبار الزامی است.");
        }

        if (warehouseId == Guid.Empty)
        {
            throw new DomainException("شناسه انبار نامعتبر است.");
        }

        if (supplierId == Guid.Empty)
        {
            throw new DomainException("شناسه تأمین‌کننده نامعتبر است.");
        }

        if (!IsReasonValidForType(voucherType, reason))
        {
            throw new DomainException("دلیل انتخاب‌شده با نوع سند انبار (ورود / خروج) هم‌خوانی ندارد.");
        }

        VoucherNumber = TrimMax(voucherNumber, MaxVoucherNumberLength)!;
        VoucherType = voucherType;
        Status = WarehouseVoucherStatus.Posted;
        Reason = reason;
        WarehouseId = warehouseId;
        SupplierId = supplierId;
        CounterpartyName = TrimMax(counterpartyName, MaxCounterpartyLength);
        ExternalReference = TrimMax(externalReference, MaxExternalReferenceLength);
        VoucherDate = voucherDate ?? DateTimeOffset.UtcNow;
        Note = TrimMax(note, MaxNoteLength);
        CreatedByName = TrimMax(createdByName, MaxActorNameLength);
        TotalAmount = 0m;
    }

    public string VoucherNumber { get; private set; } = string.Empty;

    public WarehouseVoucherType VoucherType { get; private set; }

    public WarehouseVoucherStatus Status { get; private set; } = WarehouseVoucherStatus.Posted;

    public WarehouseVoucherReason Reason { get; private set; }

    /// <summary>Null = main inventory stock (انبار اصلی); set = admin-defined warehouse/sub-warehouse.</summary>
    public Guid? WarehouseId { get; private set; }

    public Warehouse? Warehouse { get; private set; }

    public Guid? SupplierId { get; private set; }

    public Supplier? Supplier { get; private set; }

    /// <summary>Delivery person / supplier rep (Receipt) or receiver / destination department (Issue).</summary>
    public string? CounterpartyName { get; private set; }

    /// <summary>External invoice, waybill, or requisition reference number.</summary>
    public string? ExternalReference { get; private set; }

    public DateTimeOffset VoucherDate { get; private set; }

    public decimal TotalAmount { get; private set; }

    public string? Note { get; private set; }

    public string? CreatedByName { get; private set; }

    public DateTimeOffset? CancelledAt { get; private set; }

    public string? CancelledByName { get; private set; }

    public string? CancellationReason { get; private set; }

    public ICollection<WarehouseVoucherLine> Lines { get; private set; } = [];

    public WarehouseVoucherLine AddLine(
        Guid ingredientId,
        decimal quantity,
        decimal? unitPrice,
        decimal stockBefore,
        decimal stockAfter,
        string? batchNumber = null,
        DateOnly? expirationDate = null,
        string? lineNote = null)
    {
        var line = new WarehouseVoucherLine(
            Id,
            ingredientId,
            quantity,
            unitPrice,
            stockBefore,
            stockAfter,
            batchNumber,
            expirationDate,
            lineNote);

        Lines.Add(line);
        TotalAmount += line.LineTotal;
        return line;
    }

    public void Cancel(string reason, string? cancelledByName = null)
    {
        if (Status == WarehouseVoucherStatus.Cancelled)
        {
            throw new DomainException("این سند قبلاً ابطال شده است.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException("ثبت علت ابطال سند الزامی است.");
        }

        Status = WarehouseVoucherStatus.Cancelled;
        CancelledAt = DateTimeOffset.UtcNow;
        CancellationReason = TrimMax(reason, MaxCancellationReasonLength);
        CancelledByName = TrimMax(cancelledByName, MaxActorNameLength);
    }

    public static bool IsReasonValidForType(WarehouseVoucherType type, WarehouseVoucherReason reason)
    {
        var code = (int)reason;
        return type switch
        {
            WarehouseVoucherType.Receipt => code is >= 1 and < 10,
            WarehouseVoucherType.Issue => code is >= 10 and < 20,
            _ => false,
        };
    }

    public static string GetReasonLabel(WarehouseVoucherReason reason) => reason switch
    {
        WarehouseVoucherReason.Purchase => "خرید از تأمین‌کننده",
        WarehouseVoucherReason.ProductionReturn => "برگشت از تولید / آشپزخانه",
        WarehouseVoucherReason.OpeningBalance => "موجودی اول دوره",
        WarehouseVoucherReason.InventorySurplus => "اضافه انبارگردانی",
        WarehouseVoucherReason.SampleOrGift => "نمونه / هدیه / امانی",
        WarehouseVoucherReason.OtherReceipt => "سایر ورودها",
        WarehouseVoucherReason.KitchenConsumption => "مصرف روزانه بار و آشپزخانه",
        WarehouseVoucherReason.ProductionIssue => "ارسال به خط تولید / قنادی",
        WarehouseVoucherReason.WastageOrSpoilage => "ضایعات / فساد / انقضا",
        WarehouseVoucherReason.InternalConsumption => "مصرف پرسنلی / داخلی",
        WarehouseVoucherReason.ReturnToSupplier => "برگشت به تأمین‌کننده",
        WarehouseVoucherReason.InventoryShortage => "کسری انبارگردانی",
        WarehouseVoucherReason.SampleOrPromo => "نمونه / تست کیفیت",
        WarehouseVoucherReason.OtherIssue => "سایر خروج‌ها",
        _ => reason.ToString(),
    };

    private static string? TrimMax(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}

public class WarehouseVoucherLine : BaseEntity
{
    private WarehouseVoucherLine()
    {
    }

    public WarehouseVoucherLine(
        Guid voucherId,
        Guid ingredientId,
        decimal quantity,
        decimal? unitPrice,
        decimal stockBefore,
        decimal stockAfter,
        string? batchNumber = null,
        DateOnly? expirationDate = null,
        string? lineNote = null)
    {
        if (voucherId == Guid.Empty)
        {
            throw new DomainException("شناسه سند انبار الزامی است.");
        }

        if (ingredientId == Guid.Empty)
        {
            throw new DomainException("انتخاب ماده اولیه برای ردیف سند الزامی است.");
        }

        if (quantity <= 0)
        {
            throw new DomainException("مقدار هر قلم سند باید بزرگ‌تر از صفر باشد.");
        }

        if (unitPrice.HasValue && unitPrice.Value < 0)
        {
            throw new DomainException("قیمت واحد نمی‌تواند منفی باشد.");
        }

        VoucherId = voucherId;
        IngredientId = ingredientId;
        Quantity = quantity;
        UnitPrice = unitPrice;
        LineTotal = unitPrice.HasValue
            ? Math.Round(quantity * unitPrice.Value, 2, MidpointRounding.AwayFromZero)
            : 0m;
        StockBefore = stockBefore;
        StockAfter = stockAfter;
        BatchNumber = TrimMax(batchNumber, WarehouseVoucher.MaxBatchNumberLength);
        ExpirationDate = expirationDate;
        LineNote = TrimMax(lineNote, WarehouseVoucher.MaxLineNoteLength);
    }

    public Guid VoucherId { get; private set; }

    public WarehouseVoucher Voucher { get; private set; } = null!;

    public Guid IngredientId { get; private set; }

    public Ingredient Ingredient { get; private set; } = null!;

    public decimal Quantity { get; private set; }

    public decimal? UnitPrice { get; private set; }

    public decimal LineTotal { get; private set; }

    public decimal StockBefore { get; private set; }

    public decimal StockAfter { get; private set; }

    public string? BatchNumber { get; private set; }

    public DateOnly? ExpirationDate { get; private set; }

    public string? LineNote { get; private set; }

    private static string? TrimMax(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
