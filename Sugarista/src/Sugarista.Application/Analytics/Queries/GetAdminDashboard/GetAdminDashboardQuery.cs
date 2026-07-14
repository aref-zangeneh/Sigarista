using Microsoft.EntityFrameworkCore;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Analytics.Queries.GetAdminDashboard;

public record GetAdminDashboardQuery : IRequest<AdminDashboardDto>;

public record AdminDashboardDto(
    SalesSummaryDto Sales,
    FinanceSummaryDto Finance,
    OrderSummaryDto Orders,
    CustomerSummaryDto Customers,
    IReadOnlyCollection<CategoryProfitDto> ProfitByCategory,
    IReadOnlyCollection<ItemSalesDto> TopSoldItems,
    IReadOnlyCollection<ItemSalesDto> LeastSoldItems,
    IReadOnlyCollection<LowStockItemDto> LowStockItems,
    IReadOnlyCollection<WarehouseSummaryDto> WarehouseInventory,
    IReadOnlyCollection<PeakTimeDto> PeakTimes,
    IReadOnlyCollection<DebtorDto> Debtors,
    IReadOnlyCollection<CreditorDto> Creditors,
    IReadOnlyCollection<ManagementAlertDto> Alerts);

public record SalesSummaryDto(
    decimal DailySales,
    decimal WeeklySales,
    decimal MonthlySales,
    int DailyOrderCount,
    int WeeklyOrderCount,
    int MonthlyOrderCount);

public record FinanceSummaryDto(
    decimal CashBalance,
    decimal TotalReceivables,
    decimal TotalPayables);

public record OrderSummaryDto(
    int TotalOrders,
    int PaidOrders,
    int PendingOrders,
    int PartiallyPaidOrders,
    int CancelledOrders,
    decimal AverageOrderValue);

public record CustomerSummaryDto(
    int NewCustomers,
    int LoyalCustomers,
    int TotalUniqueCustomers);

public record CategoryProfitDto(
    Guid CategoryId,
    string CategoryName,
    decimal Revenue,
    decimal Cost,
    decimal Profit,
    decimal MarginPercent,
    int QuantitySold);

public record ItemSalesDto(
    Guid MenuItemId,
    string PersianName,
    string EnglishName,
    int QuantitySold,
    decimal Revenue);

public record LowStockItemDto(
    Guid IngredientId,
    string PersianName,
    string EnglishName,
    string Unit,
    string InventoryLocation,
    decimal CurrentQuantity,
    decimal MinAlertQuantity,
    decimal Shortfall);

public record WarehouseSummaryDto(
    string Location,
    string LocationLabel,
    int ItemCount,
    decimal TotalQuantity,
    int LowStockCount);

public record PeakTimeDto(
    int Hour,
    string Label,
    int OrderCount,
    decimal Revenue);

public record DebtorDto(
    string CustomerName,
    string CustomerPhone,
    string TrackingCode,
    decimal OutstandingAmount,
    string Status);

public record CreditorDto(
    string SupplierName,
    string Description,
    decimal Amount);

public record ManagementAlertDto(
    string Severity,
    string Title,
    string Message,
    string? ActionTab);

public class GetAdminDashboardQueryHandler : IRequestHandler<GetAdminDashboardQuery, AdminDashboardDto>
{
    private static readonly TimeSpan IranOffset = TimeSpan.FromHours(3.5);

    private static readonly IReadOnlyDictionary<string, string> LocationLabels = new Dictionary<string, string>
    {
        ["main"] = "انبار اصلی",
        ["bar"] = "بار",
        ["kitchen"] = "آشپزخانه",
        ["dry"] = "انبار خشک",
        ["refrigerator"] = "یخچال",
    };

    private readonly IApplicationDbContext _dbContext;

    public GetAdminDashboardQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AdminDashboardDto> Handle(GetAdminDashboardQuery request, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var localNow = now.ToOffset(IranOffset);
        var startOfDay = new DateTimeOffset(localNow.Date, IranOffset).ToUniversalTime();
        var startOfWeek = startOfDay.AddDays(-6);
        var startOfMonth = new DateTimeOffset(localNow.Date.AddDays(1 - localNow.Day), IranOffset).ToUniversalTime();

        var orders = await _dbContext.Orders
            .AsNoTracking()
            .Include(o => o.Lines)
            .ToListAsync(cancellationToken);

        var paidOrders = orders.Where(o => o.Status == OrderStatus.Paid).ToList();

        decimal RevenueInPeriod(IEnumerable<Order> source, DateTimeOffset from) =>
            source.Where(o => (o.PaidAt ?? o.CreatedAt) >= from).Sum(o => o.TotalAmount);

        int CountInPeriod(IEnumerable<Order> source, DateTimeOffset from) =>
            source.Count(o => (o.PaidAt ?? o.CreatedAt) >= from);

        var sales = new SalesSummaryDto(
            RevenueInPeriod(paidOrders, startOfDay),
            RevenueInPeriod(paidOrders, startOfWeek),
            RevenueInPeriod(paidOrders, startOfMonth),
            CountInPeriod(paidOrders, startOfDay),
            CountInPeriod(paidOrders, startOfWeek),
            CountInPeriod(paidOrders, startOfMonth));

        var cashBalance = orders.Sum(o => o.AmountPaid);

        var receivables = orders
            .Where(o => o.Status is OrderStatus.PendingPayment or OrderStatus.PartiallyPaid)
            .Sum(o => o.TotalAmount - o.AmountPaid);

        var inventoryRows = await _dbContext.InventoryStocks
            .AsNoTracking()
            .Include(s => s.Ingredient)
            .ToListAsync(cancellationToken);

        var payables = inventoryRows
            .Where(s => s.CurrentQuantity < s.MinAlertQuantity && s.Ingredient.UnitCost > 0)
            .Sum(s => (s.MinAlertQuantity - s.CurrentQuantity) * s.Ingredient.UnitCost);

        var finance = new FinanceSummaryDto(cashBalance, receivables, payables);

        var avgOrderValue = paidOrders.Count == 0
            ? 0
            : Math.Round(paidOrders.Average(o => o.TotalAmount), 2);

        var orderSummary = new OrderSummaryDto(
            orders.Count,
            orders.Count(o => o.Status == OrderStatus.Paid),
            orders.Count(o => o.Status == OrderStatus.PendingPayment),
            orders.Count(o => o.Status == OrderStatus.PartiallyPaid),
            orders.Count(o => o.Status == OrderStatus.Cancelled),
            avgOrderValue);

        var customerPhones = paidOrders
            .GroupBy(o => NormalizePhone(o.CustomerPhone))
            .Where(g => !string.IsNullOrWhiteSpace(g.Key))
            .Select(g => new { Phone = g.Key, Count = g.Count(), FirstOrder = g.Min(o => o.PaidAt ?? o.CreatedAt) })
            .ToList();

        var newCustomers = customerPhones.Count(c => c.FirstOrder >= startOfMonth);
        var loyalCustomers = customerPhones.Count(c => c.Count >= 3);

        var customers = new CustomerSummaryDto(
            newCustomers,
            loyalCustomers,
            customerPhones.Count);

        var menuItems = await _dbContext.MenuItems
            .AsNoTracking()
            .Include(m => m.Category)
            .ToDictionaryAsync(m => m.Id, cancellationToken);

        var recipes = await _dbContext.RecipeItems
            .AsNoTracking()
            .Include(r => r.Ingredient)
            .ToListAsync(cancellationToken);

        var recipesByMenuItem = recipes
            .GroupBy(r => r.MenuItemId)
            .ToDictionary(g => g.Key, g => g.ToList());

        decimal LineCost(OrderLine line)
        {
            if (!recipesByMenuItem.TryGetValue(line.MenuItemId, out var recipeLines))
            {
                return 0;
            }

            var unitCost = recipeLines.Sum(r => r.Quantity * r.Ingredient.UnitCost);
            return unitCost * line.Quantity;
        }

        var paidLines = paidOrders.SelectMany(o => o.Lines).ToList();

        var profitByCategory = paidLines
            .Where(l => menuItems.ContainsKey(l.MenuItemId))
            .GroupBy(l => menuItems[l.MenuItemId].CategoryId)
            .Select(group =>
            {
                var category = menuItems[group.First().MenuItemId].Category;
                var revenue = group.Sum(l => l.LineTotal);
                var cost = group.Sum(LineCost);
                var profit = revenue - cost;
                var margin = revenue == 0 ? 0 : Math.Round(profit / revenue * 100, 1);

                return new CategoryProfitDto(
                    category.Id,
                    category.Name,
                    revenue,
                    cost,
                    profit,
                    margin,
                    group.Sum(l => l.Quantity));
            })
            .OrderByDescending(c => c.Revenue)
            .ToList();

        var itemSales = paidLines
            .GroupBy(l => l.MenuItemId)
            .Select(group => new ItemSalesDto(
                group.Key,
                group.First().PersianName,
                group.First().EnglishName,
                group.Sum(l => l.Quantity),
                group.Sum(l => l.LineTotal)))
            .ToList();

        var topSold = itemSales
            .OrderByDescending(i => i.QuantitySold)
            .Take(5)
            .ToList();

        var leastSold = itemSales
            .Where(i => i.QuantitySold > 0)
            .OrderBy(i => i.QuantitySold)
            .Take(5)
            .ToList();

        var lowStockItems = inventoryRows
            .Where(s => s.CurrentQuantity < s.MinAlertQuantity)
            .OrderBy(s => s.CurrentQuantity - s.MinAlertQuantity)
            .Select(s => new LowStockItemDto(
                s.IngredientId,
                s.Ingredient.PersianName,
                s.Ingredient.EnglishName,
                s.Ingredient.Unit,
                s.Ingredient.InventoryLocation,
                s.CurrentQuantity,
                s.MinAlertQuantity,
                Math.Max(0, s.MinAlertQuantity - s.CurrentQuantity)))
            .ToList();

        var warehouseInventory = inventoryRows
            .GroupBy(s => s.Ingredient.InventoryLocation)
            .Select(group => new WarehouseSummaryDto(
                group.Key,
                LocationLabels.TryGetValue(group.Key, out var label) ? label : group.Key,
                group.Count(),
                group.Sum(s => s.CurrentQuantity),
                group.Count(s => s.CurrentQuantity < s.MinAlertQuantity)))
            .OrderBy(w => w.Location)
            .ToList();

        var peakTimes = paidOrders
            .GroupBy(o => (o.PaidAt ?? o.CreatedAt).ToOffset(IranOffset).Hour)
            .Select(group => new PeakTimeDto(
                group.Key,
                $"{group.Key:00}:00",
                group.Count(),
                group.Sum(o => o.TotalAmount)))
            .OrderByDescending(p => p.OrderCount)
            .Take(8)
            .ToList();

        var debtors = orders
            .Where(o => o.Status is OrderStatus.PendingPayment or OrderStatus.PartiallyPaid)
            .OrderByDescending(o => o.TotalAmount - o.AmountPaid)
            .Take(10)
            .Select(o => new DebtorDto(
                o.CustomerName,
                o.CustomerPhone,
                o.TrackingCode,
                o.TotalAmount - o.AmountPaid,
                o.Status.ToString()))
            .ToList();

        var creditors = inventoryRows
            .Where(s => s.CurrentQuantity < s.MinAlertQuantity && s.Ingredient.UnitCost > 0)
            .OrderByDescending(s => (s.MinAlertQuantity - s.CurrentQuantity) * s.Ingredient.UnitCost)
            .Take(10)
            .Select(s => new CreditorDto(
                "تأمین‌کننده مواد اولیه",
                $"خرید {s.Ingredient.PersianName} ({Math.Max(0, s.MinAlertQuantity - s.CurrentQuantity):0.##} {s.Ingredient.Unit})",
                Math.Round((s.MinAlertQuantity - s.CurrentQuantity) * s.Ingredient.UnitCost, 2)))
            .ToList();

        var today = DateOnly.FromDateTime(localNow.Date);
        var expiringPurchases = await _dbContext.InventoryPurchases
            .AsNoTracking()
            .Include(p => p.Ingredient)
            .Where(p => p.RemainingQuantity > 0 && p.ExpirationDate <= today.AddDays(7))
            .OrderBy(p => p.ExpirationDate)
            .Take(10)
            .ToListAsync(cancellationToken);

        var alerts = BuildAlerts(
            lowStockItems,
            inventoryRows,
            orders,
            menuItems,
            recipesByMenuItem,
            expiringPurchases,
            today);

        return new AdminDashboardDto(
            sales,
            finance,
            orderSummary,
            customers,
            profitByCategory,
            topSold,
            leastSold,
            lowStockItems,
            warehouseInventory,
            peakTimes,
            debtors,
            creditors,
            alerts);
    }

    private static List<ManagementAlertDto> BuildAlerts(
        IReadOnlyCollection<LowStockItemDto> lowStockItems,
        IReadOnlyCollection<InventoryStock> inventoryRows,
        IReadOnlyCollection<Order> orders,
        IReadOnlyDictionary<Guid, MenuItem> menuItems,
        IReadOnlyDictionary<Guid, List<RecipeItem>> recipesByMenuItem,
        IReadOnlyCollection<InventoryPurchase> expiringPurchases,
        DateOnly today)
    {
        var alerts = new List<ManagementAlertDto>();

        foreach (var purchase in expiringPurchases)
        {
            var isExpired = purchase.ExpirationDate < today;
            alerts.Add(new ManagementAlertDto(
                isExpired ? "critical" : "warning",
                isExpired ? "مواد منقضی‌شده" : "نزدیک به انقضا",
                $"{purchase.Ingredient.PersianName} — {purchase.RemainingQuantity:0.##} {purchase.Ingredient.Unit} — انقضا {purchase.ExpirationDate:yyyy/MM/dd}",
                "inventory-purchases"));
        }

        foreach (var item in lowStockItems.Take(5))
        {
            alerts.Add(new ManagementAlertDto(
                item.CurrentQuantity < 0 ? "critical" : "warning",
                "مواد اولیه کم‌موجود",
                $"{item.PersianName} — موجودی {item.CurrentQuantity:0.##} {item.Unit} (حداقل {item.MinAlertQuantity:0.##})",
                "inventory"));
        }

        var negativeStock = inventoryRows.Count(s => s.CurrentQuantity < 0);
        if (negativeStock > 0)
        {
            alerts.Add(new ManagementAlertDto(
                "critical",
                "موجودی منفی",
                $"{negativeStock} ماده اولیه موجودی منفی دارند.",
                "inventory"));
        }

        var stalePending = orders.Count(o =>
            o.Status == OrderStatus.PendingPayment &&
            o.CreatedAt < DateTimeOffset.UtcNow.AddHours(-2));

        if (stalePending > 0)
        {
            alerts.Add(new ManagementAlertDto(
                "warning",
                "سفارش‌های در انتظار پرداخت",
                $"{stalePending} سفارش بیش از ۲ ساعت در انتظار پرداخت هستند.",
                "orders"));
        }

        var partialPayments = orders.Count(o => o.Status == OrderStatus.PartiallyPaid);
        if (partialPayments > 0)
        {
            alerts.Add(new ManagementAlertDto(
                "info",
                "پرداخت‌های ناقص",
                $"{partialPayments} سفارش به‌صورت جزئی پرداخت شده‌اند.",
                "orders"));
        }

        var itemsWithoutRecipes = menuItems.Values
            .Count(m => m.IsActive && m.IsOrderable && !recipesByMenuItem.ContainsKey(m.Id));

        if (itemsWithoutRecipes > 0)
        {
            alerts.Add(new ManagementAlertDto(
                "info",
                "آیتم بدون دستور پخت",
                $"{itemsWithoutRecipes} آیتم فعال بدون دستور پخت هستند — سود دقیق محاسبه نمی‌شود.",
                "recipes"));
        }

        if (alerts.Count == 0)
        {
            alerts.Add(new ManagementAlertDto(
                "success",
                "همه چیز مرتب است",
                "هشدار فعالی وجود ندارد.",
                null));
        }

        return alerts;
    }

    private static string NormalizePhone(string phone) =>
        string.IsNullOrWhiteSpace(phone)
            ? string.Empty
            : new string(phone.Where(char.IsDigit).ToArray());
}
