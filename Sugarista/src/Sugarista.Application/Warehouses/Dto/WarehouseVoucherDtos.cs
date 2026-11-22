using Sugarista.Domain.Entities;

namespace Sugarista.Application.Warehouses.Dto;

public record WarehouseVoucherLineDto(
    Guid Id,
    Guid IngredientId,
    string IngredientName,
    string IngredientEnglishName,
    string Unit,
    decimal Quantity,
    decimal? UnitPrice,
    decimal LineTotal,
    decimal StockBefore,
    decimal StockAfter,
    string? BatchNumber,
    DateOnly? ExpirationDate,
    string? LineNote);

public record WarehouseVoucherDto(
    Guid Id,
    string VoucherNumber,
    string VoucherType,
    string VoucherTypeLabel,
    string Status,
    string StatusLabel,
    string Reason,
    string ReasonLabel,
    Guid? WarehouseId,
    string WarehouseName,
    Guid? SupplierId,
    string? SupplierName,
    string? CounterpartyName,
    string? ExternalReference,
    DateTimeOffset VoucherDate,
    decimal TotalAmount,
    int LineCount,
    string? Note,
    string? CreatedByName,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CancelledAt,
    string? CancelledByName,
    string? CancellationReason,
    IReadOnlyCollection<WarehouseVoucherLineDto> Lines);

public record WarehouseVoucherSummaryDto(
    int PostedReceiptsCount,
    decimal PostedReceiptsAmount,
    int PostedIssuesCount,
    decimal PostedIssuesAmount,
    int CancelledCount);

public record WarehouseVoucherListResponseDto(
    WarehouseVoucherSummaryDto Summary,
    IReadOnlyCollection<WarehouseVoucherDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

internal static class WarehouseVoucherMapper
{
    internal const string MainWarehouseDisplayName = "انبار اصلی (موجودی مرکزی)";

    internal static WarehouseVoucherDto ToDto(WarehouseVoucher voucher)
    {
        var typeLabel = voucher.VoucherType == WarehouseVoucherType.Receipt
            ? "رسید ورود به انبار"
            : "سند خروج از انبار";

        var statusLabel = voucher.Status == WarehouseVoucherStatus.Posted
            ? "ثبت قطعی"
            : "ابطال‌شده";

        var lines = voucher.Lines
            .Select(line => new WarehouseVoucherLineDto(
                line.Id,
                line.IngredientId,
                line.Ingredient?.PersianName ?? string.Empty,
                line.Ingredient?.EnglishName ?? string.Empty,
                line.Ingredient?.Unit ?? string.Empty,
                line.Quantity,
                line.UnitPrice,
                line.LineTotal,
                line.StockBefore,
                line.StockAfter,
                line.BatchNumber,
                line.ExpirationDate,
                line.LineNote))
            .ToArray();

        return new WarehouseVoucherDto(
            voucher.Id,
            voucher.VoucherNumber,
            voucher.VoucherType.ToString(),
            typeLabel,
            voucher.Status.ToString(),
            statusLabel,
            voucher.Reason.ToString(),
            WarehouseVoucher.GetReasonLabel(voucher.Reason),
            voucher.WarehouseId,
            voucher.Warehouse?.Name ?? MainWarehouseDisplayName,
            voucher.SupplierId,
            voucher.Supplier?.PersianName,
            voucher.CounterpartyName,
            voucher.ExternalReference,
            voucher.VoucherDate,
            voucher.TotalAmount,
            lines.Length,
            voucher.Note,
            voucher.CreatedByName,
            voucher.CreatedAt,
            voucher.CancelledAt,
            voucher.CancelledByName,
            voucher.CancellationReason,
            lines);
    }
}
