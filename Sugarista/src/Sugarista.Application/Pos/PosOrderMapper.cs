using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.DrinkBuilder;
using Sugarista.Application.MenuItems.Bundles;
using Sugarista.Application.MenuItems.OptionGroups;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Pos;

public static class PosOrderMapper
{
    public static IReadOnlyCollection<PosPaymentLegDto> MapPaymentLegs(Order order)
    {
        return order.Payments
            .Where(p => p.Status == OrderPaymentStatus.Paid && p.PaymentMethod != null && p.Amount > 0m)
            .OrderBy(p => p.PaidAt)
            .Select(p => new PosPaymentLegDto(
                p.PaymentMethod!.Value.ToString(),
                Order.GetPaymentMethodLabel(p.PaymentMethod.Value),
                p.Amount,
                p.ReferenceId))
            .ToArray();
    }

    public static PosOrderDetailDto ToDetail(
        Order order,
        IReadOnlyDictionary<Guid, string>? tableNames = null,
        bool includeManagerNote = false)
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
                .ToArray(),
            order.OrderKind.ToString(),
            order.ComplimentaryReason,
            order.IsExcludedFromAccounting,
            order.DiscountTotal,
            order.ManualDiscountType?.ToString(),
            order.ManualDiscountValue,
            order.ManualDiscountAmount,
            order.ManualDiscountReason,
            order.ManualDiscountAppliedBy,
            order.CustomerId,
            MapPaymentLegs(order),
            includeManagerNote ? order.ManagerNote : null);
    }

    public static PosOrderLineDto ToLine(OrderLine line)
    {
        IReadOnlyCollection<Guid> optionIds = [];
        if (!string.IsNullOrWhiteSpace(line.OptionsJson))
        {
            var snapshot = MenuItemOptionPricing.Deserialize(line.OptionsJson);
            if (snapshot?.Components is { Count: > 0 })
            {
                optionIds = snapshot.Components.Select(c => c.OptionId).ToArray();
            }
        }

        return new(
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
            [],
            line.CustomDrinkSummary,
            DrinkBuilderPricing.Deserialize(line.CustomizationJson),
            line.OptionLabels,
            optionIds,
            BundlePricingService.DeserializeSnapshot(line.BundleSnapshotJson));
    }

    public static PosOrderSummaryDto ToSummary(
        Order order,
        IReadOnlyDictionary<Guid, string>? tableNames = null,
        bool includeManagerNote = false)
    {
        string? tableName = null;
        if (order.SalonTableId.HasValue && tableNames?.TryGetValue(order.SalonTableId.Value, out tableName) != true)
        {
            tableName = null;
        }

        var cleanPhone = string.IsNullOrWhiteSpace(order.CustomerPhone) ||
                         string.Equals(order.CustomerPhone, "POS", StringComparison.OrdinalIgnoreCase)
            ? null
            : order.CustomerPhone;

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
            order.HeldAt,
            order.OrderKind.ToString(),
            order.ComplimentaryReason,
            order.IsExcludedFromAccounting,
            order.DiscountTotal,
            order.ManualDiscountType?.ToString(),
            order.ManualDiscountValue,
            order.ManualDiscountAmount,
            order.ManualDiscountReason,
            order.ManualDiscountAppliedBy,
            order.PaymentMethod?.ToString(),
            MapPaymentLegs(order),
            includeManagerNote ? order.ManagerNote : null,
            cleanPhone);
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
