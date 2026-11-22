using Microsoft.EntityFrameworkCore;
using Sugarista.Domain.Entities;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Application.MenuItems.Queries.GetMenuItemPriceHistory;

public record GetMenuItemPriceHistoryQuery(Guid MenuItemId) : IRequest<MenuItemPriceHistoryResponseDto>;

public record MenuItemPriceHistoryEntryDto(
    Guid Id,
    decimal? PreviousPurchasePrice,
    decimal? NewPurchasePrice,
    decimal? PurchasePriceDelta,
    decimal? PurchasePriceChangePercent,
    decimal? PreviousSalePrice,
    decimal? NewSalePrice,
    decimal? SalePriceDelta,
    decimal? SalePriceChangePercent,
    decimal? ProfitAtChange,
    decimal? MarginPercentAtChange,
    string Source,
    string? Note,
    string? ChangedBy,
    DateTimeOffset RecordedAt);

public record MenuItemPriceTrendSummaryDto(
    decimal? FirstPurchasePrice,
    decimal? LatestPurchasePrice,
    decimal? TotalPurchaseChange,
    decimal? TotalPurchaseChangePercent,
    decimal? MinPurchasePrice,
    decimal? MaxPurchasePrice,
    decimal? AveragePurchasePrice,
    decimal? FirstSalePrice,
    decimal? LatestSalePrice,
    decimal? TotalSaleChange,
    decimal? TotalSaleChangePercent,
    int EntryCount);

public record MenuItemPriceHistoryResponseDto(
    Guid MenuItemId,
    string PersianName,
    string EnglishName,
    decimal? CurrentPurchasePrice,
    decimal CurrentSalePrice,
    decimal? CurrentProfit,
    decimal? CurrentMarginPercent,
    decimal? CurrentMarkupPercent,
    bool TrackInventory,
    decimal? StockQuantity,
    decimal? MinStockAlert,
    MenuItemPriceTrendSummaryDto Summary,
    IReadOnlyCollection<MenuItemPriceHistoryEntryDto> Entries);

public class GetMenuItemPriceHistoryQueryHandler
    : IRequestHandler<GetMenuItemPriceHistoryQuery, MenuItemPriceHistoryResponseDto>
{
    private readonly IApplicationDbContext _dbContext;

    public GetMenuItemPriceHistoryQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<MenuItemPriceHistoryResponseDto> Handle(
        GetMenuItemPriceHistoryQuery request,
        CancellationToken cancellationToken)
    {
        var item = await _dbContext.MenuItems
            .AsNoTracking()
            .Where(i => i.Id == request.MenuItemId)
            .Select(i => new
            {
                i.Id,
                i.PersianName,
                i.EnglishName,
                i.PurchasePrice,
                i.Price
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new DomainException("آیتم منو یافت نشد.");

        var stock = await _dbContext.MenuItemStocks
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.MenuItemId == item.Id, cancellationToken);

        var rawEntries = await _dbContext.MenuItemPriceHistories
            .AsNoTracking()
            .Where(h => h.MenuItemId == item.Id)
            .OrderByDescending(h => h.RecordedAt)
            .ThenByDescending(h => h.Id)
            .ToListAsync(cancellationToken);

        var currentProfit = item.PurchasePrice.HasValue
            ? item.Price - item.PurchasePrice.Value
            : (decimal?)null;
        var currentMarginPercent = item.PurchasePrice.HasValue && item.Price > 0m
            ? Math.Round(((item.Price - item.PurchasePrice.Value) / item.Price) * 100m, 1)
            : (decimal?)null;
        var currentMarkupPercent = item.PurchasePrice is { } pp && pp > 0m
            ? Math.Round(((item.Price - pp) / pp) * 100m, 1)
            : (decimal?)null;

        var summary = ComputeTrendSummary(rawEntries, item.PurchasePrice, item.Price);

        var entryDtos = rawEntries
            .Select(entry =>
            {
                decimal? profitAtChange = entry.NewPurchasePrice.HasValue && entry.NewSalePrice.HasValue
                    ? entry.NewSalePrice.Value - entry.NewPurchasePrice.Value
                    : null;
                decimal? marginAtChange = profitAtChange.HasValue && entry.NewSalePrice is { } sp && sp > 0m
                    ? Math.Round((profitAtChange.Value / sp) * 100m, 1)
                    : null;

                return new MenuItemPriceHistoryEntryDto(
                    entry.Id,
                    entry.PreviousPurchasePrice,
                    entry.NewPurchasePrice,
                    entry.PurchasePriceDelta,
                    entry.PurchasePriceChangePercent,
                    entry.PreviousSalePrice,
                    entry.NewSalePrice,
                    entry.SalePriceDelta,
                    entry.SalePriceChangePercent,
                    profitAtChange,
                    marginAtChange,
                    entry.Source,
                    entry.Note,
                    entry.ChangedBy,
                    entry.RecordedAt);
            })
            .ToList();

        return new MenuItemPriceHistoryResponseDto(
            item.Id,
            item.PersianName,
            item.EnglishName,
            item.PurchasePrice,
            item.Price,
            currentProfit,
            currentMarginPercent,
            currentMarkupPercent,
            stock is not null,
            stock?.CurrentQuantity,
            stock?.MinAlertQuantity,
            summary,
            entryDtos);
    }

    internal static MenuItemPriceTrendSummaryDto ComputeTrendSummary(
        IReadOnlyList<MenuItemPriceHistory> entriesNewestFirst,
        decimal? currentPurchasePrice,
        decimal currentSalePrice)
    {
        // Build chronological price observations oldest -> newest.
        var chronological = entriesNewestFirst.Reverse().ToList();

        var purchaseSeries = new List<decimal>();
        var saleSeries = new List<decimal>();

        for (var i = 0; i < chronological.Count; i++)
        {
            var e = chronological[i];
            if (i == 0 && e.PreviousPurchasePrice.HasValue)
            {
                purchaseSeries.Add(e.PreviousPurchasePrice.Value);
            }
            if (e.NewPurchasePrice.HasValue)
            {
                purchaseSeries.Add(e.NewPurchasePrice.Value);
            }

            if (i == 0 && e.PreviousSalePrice.HasValue)
            {
                saleSeries.Add(e.PreviousSalePrice.Value);
            }
            if (e.NewSalePrice.HasValue)
            {
                saleSeries.Add(e.NewSalePrice.Value);
            }
        }

        if (purchaseSeries.Count == 0 && currentPurchasePrice.HasValue)
        {
            purchaseSeries.Add(currentPurchasePrice.Value);
        }

        if (saleSeries.Count == 0 && currentSalePrice > 0m)
        {
            saleSeries.Add(currentSalePrice);
        }

        decimal? firstPurchase = purchaseSeries.Count > 0 ? purchaseSeries[0] : null;
        decimal? latestPurchase = purchaseSeries.Count > 0 ? purchaseSeries[^1] : currentPurchasePrice;
        decimal? totalPurchaseChange = firstPurchase.HasValue && latestPurchase.HasValue
            ? latestPurchase.Value - firstPurchase.Value
            : null;
        decimal? totalPurchasePct = firstPurchase is { } fp && fp > 0m && latestPurchase.HasValue
            ? Math.Round(((latestPurchase.Value - fp) / fp) * 100m, 2)
            : null;

        decimal? minPurchase = purchaseSeries.Count > 0 ? purchaseSeries.Min() : null;
        decimal? maxPurchase = purchaseSeries.Count > 0 ? purchaseSeries.Max() : null;
        decimal? avgPurchase = purchaseSeries.Count > 0 ? Math.Round(purchaseSeries.Average(), 0) : null;

        decimal? firstSale = saleSeries.Count > 0 ? saleSeries[0] : null;
        decimal? latestSale = saleSeries.Count > 0 ? saleSeries[^1] : currentSalePrice;
        decimal? totalSaleChange = firstSale.HasValue && latestSale.HasValue
            ? latestSale.Value - firstSale.Value
            : null;
        decimal? totalSalePct = firstSale is { } fs && fs > 0m && latestSale.HasValue
            ? Math.Round(((latestSale.Value - fs) / fs) * 100m, 2)
            : null;

        return new MenuItemPriceTrendSummaryDto(
            firstPurchase,
            latestPurchase,
            totalPurchaseChange,
            totalPurchasePct,
            minPurchase,
            maxPurchase,
            avgPurchase,
            firstSale,
            latestSale,
            totalSaleChange,
            totalSalePct,
            entriesNewestFirst.Count);
    }
}
