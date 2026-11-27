using System.Text.Json;
using System.Text.Json.Serialization;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Affiliates.Services;

public sealed record AffiliateCommissionSnapshotLine(
    Guid MenuItemId,
    string MenuItemName,
    Guid CategoryId,
    string? CategoryName,
    int Quantity,
    decimal UnitPriceAtOrder,
    decimal LineSubtotal,
    decimal LineDiscountShare,
    decimal NetEligibleAmount,
    decimal AppliedRatePercentage,
    EffectiveCommissionSource RateSource,
    decimal LineCommissionAmount);

/// <summary>
/// Immutable financial snapshot locked at the exact moment an order is placed.
/// Future changes to menu item prices, category commission percentages, or
/// affiliate tiers can never retroactively alter a locked commission.
/// </summary>
public sealed record AffiliateCommissionSnapshot(
    Guid OrderId,
    string OrderTrackingCode,
    DateTimeOffset SnapshotCreatedAt,
    Guid? TierIdAtOrder,
    string? TierNameAtOrder,
    decimal? CustomCommissionRateAtOrder,
    decimal OrderGrossItemsSubtotal,
    decimal OrderDiscountTotal,
    decimal EligibleAmount,
    decimal CommissionAmount,
    IReadOnlyList<AffiliateCommissionSnapshotLine> Lines)
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    public string ToJson() => JsonSerializer.Serialize(this, SerializerOptions);

    public static AffiliateCommissionSnapshot? FromJson(string? json) =>
        string.IsNullOrWhiteSpace(json)
            ? null
            : JsonSerializer.Deserialize<AffiliateCommissionSnapshot>(json, SerializerOptions);

    public static AffiliateCommissionSnapshot Build(
        Order order,
        Affiliate affiliate,
        AffiliateCommissionCalculationResult calculation,
        IReadOnlyDictionary<Guid, string> categoryNamesById,
        DateTimeOffset snapshotCreatedAt)
    {
        var orderLinesList = order.Lines.ToList();
        var snapshotLines = new List<AffiliateCommissionSnapshotLine>(calculation.Lines.Count);

        for (var i = 0; i < calculation.Lines.Count; i++)
        {
            var calcLine = calculation.Lines[i];
            var orderLine = i < orderLinesList.Count ? orderLinesList[i] : null;
            categoryNamesById.TryGetValue(calcLine.CategoryId, out var categoryName);

            snapshotLines.Add(new AffiliateCommissionSnapshotLine(
                calcLine.MenuItemId,
                orderLine?.PersianName ?? string.Empty,
                calcLine.CategoryId,
                categoryName,
                orderLine?.Quantity ?? 1,
                orderLine?.UnitPrice ?? calcLine.LineSubtotal,
                calcLine.LineSubtotal,
                calcLine.LineDiscountShare,
                calcLine.NetEligibleAmount,
                calcLine.AppliedRatePercentage,
                calcLine.RateSource,
                calcLine.CommissionAmount));
        }

        return new AffiliateCommissionSnapshot(
            order.Id,
            order.TrackingCode,
            snapshotCreatedAt,
            affiliate.TierId,
            affiliate.Tier?.Name,
            affiliate.CustomCommissionRate,
            calculation.Lines.Sum(l => l.LineSubtotal),
            order.DiscountTotal,
            calculation.EligibleAmount,
            calculation.CommissionAmount,
            snapshotLines);
    }
}
