using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

/// <summary>
/// Immutable price-change history record for a <see cref="MenuItem"/>, tracking both
/// purchase cost (<see cref="MenuItem.PurchasePrice"/>) and selling price (<see cref="MenuItem.Price"/>)
/// over time so managers can inspect supplier cost inflation, margin erosion, and price trends.
/// </summary>
public class MenuItemPriceHistory : BaseEntity
{
    public const string SourceInitial = "Initial";
    public const string SourceManualEdit = "ManualEdit";
    public const string SourceAutoPricing = "AutoPricing";
    public const string SourceDuplicate = "Duplicate";

    private MenuItemPriceHistory()
    {
    }

    private MenuItemPriceHistory(
        Guid menuItemId,
        decimal? previousPurchasePrice,
        decimal? newPurchasePrice,
        decimal? previousSalePrice,
        decimal? newSalePrice,
        string source,
        string? note,
        string? changedBy,
        DateTimeOffset recordedAt)
    {
        if (menuItemId == Guid.Empty)
        {
            throw new DomainException("شناسه آیتم منو برای ثبت تاریخچه قیمت الزامی است.");
        }

        MenuItemId = menuItemId;
        PreviousPurchasePrice = previousPurchasePrice;
        NewPurchasePrice = newPurchasePrice;
        PurchasePriceDelta = ComputeDelta(previousPurchasePrice, newPurchasePrice);
        PurchasePriceChangePercent = ComputePercent(previousPurchasePrice, newPurchasePrice);

        PreviousSalePrice = previousSalePrice;
        NewSalePrice = newSalePrice;
        SalePriceDelta = ComputeDelta(previousSalePrice, newSalePrice);
        SalePriceChangePercent = ComputePercent(previousSalePrice, newSalePrice);

        Source = string.IsNullOrWhiteSpace(source) ? SourceManualEdit : source.Trim();
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (Note is { Length: > 500 })
        {
            Note = Note[..500];
        }

        ChangedBy = string.IsNullOrWhiteSpace(changedBy) ? null : changedBy.Trim();
        if (ChangedBy is { Length: > 150 })
        {
            ChangedBy = ChangedBy[..150];
        }

        RecordedAt = recordedAt;
    }

    public Guid MenuItemId { get; private set; }

    public MenuItem MenuItem { get; private set; } = null!;

    public decimal? PreviousPurchasePrice { get; private set; }

    public decimal? NewPurchasePrice { get; private set; }

    public decimal? PurchasePriceDelta { get; private set; }

    public decimal? PurchasePriceChangePercent { get; private set; }

    public decimal? PreviousSalePrice { get; private set; }

    public decimal? NewSalePrice { get; private set; }

    public decimal? SalePriceDelta { get; private set; }

    public decimal? SalePriceChangePercent { get; private set; }

    public string Source { get; private set; } = SourceManualEdit;

    public string? Note { get; private set; }

    public string? ChangedBy { get; private set; }

    public DateTimeOffset RecordedAt { get; private set; }

    public static MenuItemPriceHistory Create(
        Guid menuItemId,
        decimal? previousPurchasePrice,
        decimal? newPurchasePrice,
        decimal? previousSalePrice,
        decimal? newSalePrice,
        string source = SourceManualEdit,
        string? note = null,
        string? changedBy = null,
        DateTimeOffset? recordedAt = null)
    {
        return new MenuItemPriceHistory(
            menuItemId,
            previousPurchasePrice,
            newPurchasePrice,
            previousSalePrice,
            newSalePrice,
            source,
            note,
            changedBy,
            recordedAt ?? DateTimeOffset.UtcNow);
    }

    private static decimal? ComputeDelta(decimal? previous, decimal? current)
    {
        if (!previous.HasValue || !current.HasValue)
        {
            return null;
        }

        return current.Value - previous.Value;
    }

    private static decimal? ComputePercent(decimal? previous, decimal? current)
    {
        if (!previous.HasValue || !current.HasValue || previous.Value <= 0m)
        {
            return null;
        }

        return Math.Round(((current.Value - previous.Value) / previous.Value) * 100m, 2);
    }
}
