using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Pos;

internal static class PosOrderMapper
{
    internal static PosOrderDetailDto ToDetail(Order order, IReadOnlyDictionary<Guid, string>? tableNames = null)
    {
        var tableName = order.SalonTableId.HasValue && tableNames?.TryGetValue(order.SalonTableId.Value, out var name) == true
            ? name
            : null;

        return new PosOrderDetailDto(
            order.Id,
            order.TrackingCode,
            order.CustomerName,
            order.CustomerPhone,
            order.Note,
            order.Status.ToString(),
            order.ServiceType?.ToString(),
            order.PaymentMethod?.ToString(),
            order.TotalAmount,
            order.AmountPaid,
            order.IsSplitBill,
            order.SplitBillMode.ToString(),
            order.GuestCount,
            order.SalonTableId,
            tableName,
            order.TableSessionId,
            order.DeliveryMethodId,
            order.DeliveryMethod?.PersianName,
            order.DeliveryFee,
            order.CreatedAt,
            order.HeldAt,
            order.Lines.Select(ToLine).ToArray(),
            order.SplitShares
                .OrderBy(share => share.SortOrder)
                .Select(share => new PosSplitShareDto(
                    share.PayerKey,
                    share.PayerLabel,
                    share.SharePercentage,
                    share.ShareAmount))
                .ToArray());
    }

    internal static PosOrderLineDto ToLine(OrderLine line) =>
        new(
            line.Id,
            line.MenuItemId,
            line.PersianName,
            line.EnglishName,
            line.SizeKey,
            line.SizeLabel,
            line.Quantity,
            line.UnitPrice,
            line.LineTotal,
            line.BaristaNote,
            line.PayerKey,
            line.PayerLabel,
            line.ModifierLabels,
            []);

    internal static PosOrderSummaryDto ToSummary(Order order, IReadOnlyDictionary<Guid, string>? tableNames = null)
    {
        string? tableName = null;
        if (order.SalonTableId.HasValue && tableNames?.TryGetValue(order.SalonTableId.Value, out tableName) != true)
        {
            tableName = null;
        }

        return new PosOrderSummaryDto(
            order.Id,
            order.TrackingCode,
            order.CustomerName,
            order.ServiceType?.ToString(),
            order.Status.ToString(),
            order.TotalAmount,
            order.AmountPaid,
            order.Lines.Count,
            order.SalonTableId,
            tableName,
            order.CreatedAt,
            order.HeldAt);
    }

    internal static async Task<IReadOnlyDictionary<Guid, string>> LoadTableNamesAsync(
        IApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        return await dbContext.SalonTables
            .AsNoTracking()
            .ToDictionaryAsync(table => table.Id, table => table.Name, cancellationToken);
    }
}
