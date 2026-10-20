using System.Globalization;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Accounting.Queries.GetBalanceSheet;
using Sugarista.Application.Accounting.Queries.GetIncomeStatement;
using Sugarista.Application.Accounting.Queries.GetTrialBalance;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Exports;

/// <summary>
/// Builds the actual Excel workbook for a kind/period. Loads data in batches with
/// <c>AsAsyncEnumerable</c> (never one giant ToListAsync), honours cancellation and
/// enforces a per-sheet row cap so very large datasets cannot exhaust the server.
/// </summary>
public sealed class ExportDataBuilder(IApplicationDbContext dbContext, ISender sender)
{
    private readonly IApplicationDbContext _dbContext = dbContext;
    private readonly ISender _sender = sender;

    private const int MaxRowsPerSheet = 100_000;
    private const int BatchSize = 2_000;

    private sealed class SheetAccumulator
    {
        public List<IReadOnlyList<object?>> Rows { get; } = [];

        public bool Truncated { get; private set; }

        public void Add(IReadOnlyList<object?> row)
        {
            if (Rows.Count >= MaxRowsPerSheet)
            {
                Truncated = true;
                return;
            }

            Rows.Add(row);
        }
    }

    public async Task<(string Title, IReadOnlyCollection<ExcelSheet> Sheets, long RowsCount)> BuildAsync(
        string kind,
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken)
    {
        var (title, sheets) = kind switch
        {
            "orders" => await BuildOrdersAsync(from, to, cancellationToken),
            "customers" => await BuildCustomersAsync(cancellationToken),
            "menu-items" => await BuildMenuItemsAsync(cancellationToken),
            "recipes" => await BuildRecipesAsync(cancellationToken),
            "inventory" => await BuildInventoryAsync(cancellationToken),
            "movements" => await BuildMovementsAsync(from, to, cancellationToken),
            "purchases" => await BuildPurchasesAsync(from, to, cancellationToken),
            "suppliers" => await BuildSuppliersAsync(cancellationToken),
            "delivery-methods" => await BuildDeliveryMethodsAsync(cancellationToken),
            "accounting" => await BuildAccountingAsync(from, to, cancellationToken),
            _ => throw new InvalidOperationException($"Unknown export kind '{kind}'."),
        };

        var rowsCount = sheets.Sum(sheet => (long)sheet.Rows.Count);
        return (title, sheets, rowsCount);
    }

    private static string Shamsi(DateOnly value) => Shamsi(value.ToDateTime(TimeOnly.MinValue));

    private static string Shamsi(DateTimeOffset value) => Shamsi(value.DateTime);

    private static string Shamsi(DateTime value)
    {
        var calendar = new PersianCalendar();
        return $"{calendar.GetYear(value):0000}/{calendar.GetMonth(value):00}/{calendar.GetDayOfMonth(value):00}";
    }

    private static string PeriodSuffix(DateOnly? from, DateOnly? to)
    {
        if (from.HasValue && to.HasValue)
        {
            return $" از {Shamsi(from.Value)} تا {Shamsi(to.Value)}";
        }

        if (from.HasValue)
        {
            return $" از {Shamsi(from.Value)}";
        }

        if (to.HasValue)
        {
            return $" تا {Shamsi(to.Value)}";
        }

        return string.Empty;
    }

    private async Task<(string, IReadOnlyCollection<ExcelSheet>)> BuildOrdersAsync(
        DateOnly? from, DateOnly? to, CancellationToken cancellationToken)
    {
        var query = _dbContext.Orders
            .AsNoTracking()
            .Include(order => order.Lines)
            .Include(order => order.Payments)
            .Include(order => order.DeliveryMethod)
            .AsQueryable();

        if (from.HasValue)
        {
            query = query.Where(order => order.CreatedAt >= from.Value.ToDateTime(TimeOnly.MinValue));
        }

        if (to.HasValue)
        {
            query = query.Where(order => order.CreatedAt <= to.Value.ToDateTime(new TimeOnly(23, 59, 59)));
        }

        var orders = new SheetAccumulator();
        var lines = new SheetAccumulator();
        var payments = new SheetAccumulator();

        // streaming materialization in server-side batches — no giant ToListAsync
        await foreach (var order in query
            .OrderBy(order => order.CreatedAt)
            .AsAsyncEnumerable()
            .WithCancellation(cancellationToken))
        {
            var itemsTotal = order.TotalAmount - order.DeliveryFee - order.TaxTotal;
            orders.Add(new List<object?>
            {
                order.TrackingCode,
                Shamsi(order.CreatedAt),
                order.CustomerName,
                order.CustomerPhone,
                StatusFa(order.Status.ToString()),
                FulfillmentFa(order.FulfillmentStatus.ToString()),
                order.Channel == OrderChannel.Pos ? "صندوق فروش" : "سفارش آنلاین",
                ServiceTypeFa(order.ServiceType?.ToString()),
                order.DeliveryMethod?.PersianName ?? string.Empty,
                itemsTotal,
                order.TaxTotal,
                order.DeliveryFee,
                order.TotalAmount,
                order.AmountPaid,
                order.TotalAmount - order.AmountPaid,
                order.IsSplitBill ? "بله (تسویه جداگانه)" : "خیر",
                order.PaymentMethod?.ToString() ?? string.Empty,
                order.PaidAt.HasValue ? Shamsi(order.PaidAt.Value) : string.Empty,
                order.Note ?? string.Empty,
            });

            foreach (var line in order.Lines)
            {
                lines.Add(new List<object?>
                {
                    order.TrackingCode,
                    line.PersianName,
                    line.EnglishName,
                    line.SizeLabel ?? string.Empty,
                    line.Quantity,
                    line.UnitPrice,
                    line.LineTotal,
                    line.TaxPercent,
                    line.PayerLabel ?? string.Empty,
                    line.BaristaNote ?? string.Empty,
                    line.InventoryDeductedQuantity,
                });
            }

            foreach (var payment in order.Payments)
            {
                payments.Add(new List<object?>
                {
                    order.TrackingCode,
                    payment.PayerLabel,
                    payment.Amount,
                    payment.Status.ToString(),
                    payment.PaidAt.HasValue ? Shamsi(payment.PaidAt.Value) : string.Empty,
                });
            }
        }

        var title = $"سفارش‌ها و فاکتورها{PeriodSuffix(from, to)}";
        return (title, new List<ExcelSheet>
        {
            new("سفارش‌ها", new[]
            {
                "کد پیگیری", "تاریخ ثبت", "مشتری", "شماره تماس", "وضعیت پرداخت", "وضعیت آماده‌سازی",
                "کانال", "نوع سرویس", "روش تحویل", "مبلغ آیتم‌ها", "مالیات", "هزینه تحویل",
                "مبلغ کل (تومان)", "پرداخت‌شده (تومان)", "مانده (تومان)", "تسویه جداگانه",
                "روش پرداخت", "زمان پرداخت", "توضیحات",
            }, orders.Rows),
            new("ردیف سفارش‌ها", new[]
            {
                "کد پیگیری", "آیتم", "نام انگلیسی", "سایز", "تعداد", "قیمت واحد (تومان)",
                "جمع ردیف (تومان)", "درصد مالیات", "پرداخت‌کننده (دُنگ)", "یادداشت باریستا", "موجودی کسرشده",
            }, lines.Rows),
            new("پرداخت‌ها", new[]
            {
                "کد پیگیری", "پرداخت‌کننده", "مبلغ (تومان)", "وضعیت", "زمان پرداخت",
            }, payments.Rows),
        });
    }

    private async Task<(string, IReadOnlyCollection<ExcelSheet>)> BuildCustomersAsync(
        CancellationToken cancellationToken)
    {
        var rows = new SheetAccumulator();

        await foreach (var profile in _dbContext.Customers
            .AsNoTracking()
            .Select(customer => new
            {
                customer.PersianName,
                customer.PhoneNumber,
                customer.Email,
                customer.Notes,
                customer.FirstChannel,
                customer.LastChannel,
                customer.IsActive,
                customer.CreatedAt,
                LastOrderAt = (DateTimeOffset?)customer.Orders.Select(order => order.CreatedAt).DefaultIfEmpty().Max(),
                TotalOrders = customer.Orders.Count,
                PaidOrders = customer.Orders.Count(order => order.Status == OrderStatus.Paid),
                PaidSum = customer.Orders.Where(order => order.Status == OrderStatus.Paid).Sum(order => order.TotalAmount),
                TaxSum = customer.Orders.Where(order => order.Status == OrderStatus.Paid).Sum(order => order.TaxTotal),
            })
            .AsAsyncEnumerable()
            .WithCancellation(cancellationToken))
        {
            rows.Add(new List<object?>
            {
                profile.PersianName,
                profile.PhoneNumber,
                profile.Email ?? string.Empty,
                ChannelFa(profile.FirstChannel),
                ChannelFa(profile.LastChannel),
                profile.IsActive ? "فعال" : "غیرفعال",
                profile.TotalOrders,
                profile.PaidOrders,
                profile.PaidSum,
                profile.TaxSum,
                profile.PaidOrders > 0 ? profile.PaidSum / profile.PaidOrders : 0m,
                (int)(profile.PaidSum / 10000m),
                Shamsi(profile.CreatedAt),
                profile.LastOrderAt.HasValue ? Shamsi(profile.LastOrderAt.Value) : string.Empty,
                profile.Notes ?? string.Empty,
            });
        }

        return ("باشگاه مشتریان (یکتای تمام کانال‌ها)", new List<ExcelSheet>
        {
            new("مشتریان", new[]
            {
                "نام", "شماره تماس", "ایمیل", "کانال اول", "کانال آخر", "وضعیت",
                "کل سفارش‌ها", "سفارش‌های پرداخت‌شده", "مجموع خرید (تومان)", "مالیات پرداختی (تومان)",
                "میانگین هر سفارش (تومان)", "امتیاز وفاداری", "تاریخ عضویت", "آخرین خرید", "یادداشت",
            }, rows.Rows),
        });
    }

    private async Task<(string, IReadOnlyCollection<ExcelSheet>)> BuildMenuItemsAsync(
        CancellationToken cancellationToken)
    {
        var items = await _dbContext.MenuItems
            .AsNoTracking()
            .Include(item => item.Category)
            .Include(item => item.RecipeItems)!
            .ThenInclude(recipeItem => recipeItem.Ingredient)
            .OrderBy(item => item.DisplayOrder)
            .ToListAsync(cancellationToken);

        var itemStocks = await _dbContext.MenuItemStocks
            .AsNoTracking()
            .ToDictionaryAsync(stock => stock.MenuItemId, stock => stock, cancellationToken);

        var rows = new List<IReadOnlyList<object?>>();

        foreach (var item in items)
        {
            rows.Add(new List<object?>
            {
                item.PersianName,
                item.EnglishName,
                item.Category?.Name ?? string.Empty,
                item.Price,
                item.SmallPrice,
                item.MediumPrice,
                item.LargePrice,
                item.SpecialOfferPrice,
                item.TaxPercent,
                item.MarkupPercent,
                item.Calories,
                item.PreparingDurationMinutes,
                item.IsActive ? "فعال" : "غیرفعال",
                itemStocks.TryGetValue(item.Id, out var stock) ? stock.CurrentQuantity : null,
                itemStocks.TryGetValue(item.Id, out var stockAlert) ? stockAlert.MinAlertQuantity : null,
                string.Join(" | ", (item.RecipeItems ?? new List<RecipeItem>())
                    .Select(recipeItem => $"{recipeItem.Ingredient.PersianName}: {recipeItem.Quantity}")),
            });
        }

        return ("آیتم‌های منو و قیمت‌گذاری", new List<ExcelSheet>
        {
            new("آیتم‌های منو", new[]
            {
                "نام فارسی", "نام انگلیسی", "دسته‌بندی", "قیمت پایه (تومان)", "قیمت کوچک", "قیمت متوسط",
                "قیمت بزرگ", "قیمت پیشنهاد ویژه", "درصد مالیات", "درصد سود", "کالری", "دقیقه آماده‌سازی",
                "وضعیت", "موجودی فعلی", "حد هشدار موجودی", "فرمولاسیون",
            }, rows),
        });
    }

    private async Task<(string, IReadOnlyCollection<ExcelSheet>)> BuildRecipesAsync(
        CancellationToken cancellationToken)
    {
        var rows = new SheetAccumulator();

        await foreach (var recipeItem in _dbContext.RecipeItems
            .AsNoTracking()
            .Include(recipeItem => recipeItem.MenuItem)
            .Include(recipeItem => recipeItem.Ingredient)
            .AsAsyncEnumerable()
            .WithCancellation(cancellationToken))
        {
            rows.Add(new List<object?>
            {
                recipeItem.MenuItem.PersianName,
                recipeItem.Ingredient.PersianName,
                IngredientKindFa(recipeItem.Ingredient.Kind),
                recipeItem.Ingredient.Unit,
                recipeItem.Quantity,
                recipeItem.Ingredient.UnitCost,
                recipeItem.Quantity * recipeItem.Ingredient.UnitCost,
                recipeItem.PreparationNote ?? string.Empty,
            });
        }

        return ("دستورهای پخت", new List<ExcelSheet>
        {
            new("دستور پخت", new[]
            {
                "آیتم منو", "پیمانه", "نوع پیمانه", "واحد", "مقدار", "بهای واحد (تومان)",
                "بهای ردیف (تومان)", "یادداشت آماده‌سازی",
            }, rows.Rows),
        });
    }

    private async Task<(string, IReadOnlyCollection<ExcelSheet>)> BuildInventoryAsync(
        CancellationToken cancellationToken)
    {
        var rows = new SheetAccumulator();

        await foreach (var ingredient in _dbContext.Ingredients
            .AsNoTracking()
            .Include(ingredient => ingredient.Stock)
            .Include(ingredient => ingredient.IngredientSuppliers)!
            .ThenInclude(link => link.Supplier)
            .AsAsyncEnumerable()
            .WithCancellation(cancellationToken))
        {
            rows.Add(new List<object?>
            {
                ingredient.PersianName,
                ingredient.EnglishName,
                IngredientKindFa(ingredient.Kind),
                ingredient.Unit,
                ingredient.UnitCost,
                ingredient.InventoryLocation,
                ingredient.IsActive ? "فعال" : "غیرفعال",
                ingredient.Stock != null ? ingredient.Stock.CurrentQuantity : 0m,
                ingredient.Stock != null ? ingredient.Stock.MinAlertQuantity : 0m,
                ingredient.Stock != null ? ingredient.Stock.CurrentQuantity * ingredient.UnitCost : 0m,
                ingredient.Stock != null ? Shamsi(ingredient.Stock.LastUpdatedAt) : string.Empty,
                string.Join(" | ", ingredient.IngredientSuppliers
                    .Where(link => link.Supplier != null)
                    .Select(link => link.Supplier!.PersianName)),
            });
        }

        return ("انبار موجودی (متریال و بسته‌بندی)", new List<ExcelSheet>
        {
            new("موجودی انبار", new[]
            {
                "نام", "نام انگلیسی", "نوع", "واحد", "بهای واحد (تومان)", "انبار",
                "وضعیت", "موجودی فعلی", "حد هشدار", "ارزش موجودی (تومان)", "بروزرسانی", "تامین‌کنندگان",
            }, rows.Rows),
        });
    }

    private async Task<(string, IReadOnlyCollection<ExcelSheet>)> BuildMovementsAsync(
        DateOnly? from, DateOnly? to, CancellationToken cancellationToken)
    {
        var query = _dbContext.StockMovements
            .AsNoTracking()
            .AsQueryable();

        if (from.HasValue)
        {
            query = query.Where(movement => movement.OccurredAt >= from.Value.ToDateTime(TimeOnly.MinValue));
        }

        if (to.HasValue)
        {
            query = query.Where(movement => movement.OccurredAt <= to.Value.ToDateTime(new TimeOnly(23, 59, 59)));
        }

        var movements = new SheetAccumulator();

        await foreach (var entry in query
            .Join(_dbContext.Ingredients.AsNoTracking(),
                movement => movement.IngredientId,
                ingredient => ingredient.Id,
                (movement, ingredient) => new { movement, ingredient })
            .OrderBy(entry => entry.movement.OccurredAt)
            .AsAsyncEnumerable()
            .WithCancellation(cancellationToken))
        {
            movements.Add(new List<object?>
            {
                Shamsi(entry.movement.OccurredAt),
                MovementFa(entry.movement.MovementType.ToString()),
                entry.ingredient.PersianName,
                entry.ingredient.Unit,
                entry.movement.Quantity,
                entry.movement.SourceMenuItemName,
                entry.movement.Note,
                entry.movement.OrderId,
                entry.movement.ProductionBatchId,
            });
        }

        return ($"گردش انبار{PeriodSuffix(from, to)}", new List<ExcelSheet>
        {
            new("گردش انبار", new[]
            {
                "تاریخ", "نوع حرکت", "پیمانه", "واحد", "تغییر موجودی", "آیتم فروش (منبع)",
                "یادداشت", "شناسه سفارش", "شناسه بچ تولید",
            }, movements.Rows),
        });
    }

    private async Task<(string, IReadOnlyCollection<ExcelSheet>)> BuildPurchasesAsync(
        DateOnly? from, DateOnly? to, CancellationToken cancellationToken)
    {
        var query = _dbContext.InventoryPurchases
            .AsNoTracking()
            .Include(purchase => purchase.Ingredient)
            .Include(purchase => purchase.Supplier)
            .AsQueryable();

        if (from.HasValue)
        {
            query = query.Where(purchase => purchase.CreatedAt >= from.Value.ToDateTime(TimeOnly.MinValue));
        }

        if (to.HasValue)
        {
            query = query.Where(purchase => purchase.CreatedAt <= to.Value.ToDateTime(new TimeOnly(23, 59, 59)));
        }

        var purchases = new SheetAccumulator();
        var totalCost = 0m;
        var totalTax = 0m;
        var totalShipping = 0m;

        await foreach (var purchase in query
            .OrderBy(purchase => purchase.CreatedAt)
            .AsAsyncEnumerable()
            .WithCancellation(cancellationToken))
        {
            totalCost += purchase.TotalCost;
            totalTax += purchase.TaxAmount;
            totalShipping += purchase.ShippingFee;

            purchases.Add(new List<object?>
            {
                Shamsi(purchase.CreatedAt),
                purchase.Ingredient.PersianName,
                purchase.Supplier?.PersianName ?? string.Empty,
                purchase.Quantity,
                purchase.PackageCount,
                purchase.PackageSize,
                purchase.PurchaseUnitPrice,
                purchase.ShippingFee,
                purchase.TaxAmount,
                purchase.IncidentalCost,
                purchase.TotalCost,
                purchase.RemainingQuantity,
                Shamsi(purchase.ExpirationDate),
                purchase.Description,
            });
        }

        var summaryRows = new List<IReadOnlyList<object?>>
        {
            new List<object?> { purchases.Rows.Count, totalCost, totalTax, totalShipping },
        };

        return ($"خرید مواد اولیه{PeriodSuffix(from, to)}", new List<ExcelSheet>
        {
            new("خریدها", new[]
            {
                "تاریخ", "پیمانه", "تامین‌کننده", "مقدار", "تعداد بسته", "حجم هر بسته",
                "بهای خرید واحد", "هزینه حمل", "مالیات خرید", "هزینه متفرقه", "بهای کل (تومان)",
                "مانده مصرف‌نشده", "تاریخ انقضا", "توضیحات",
            }, purchases.Rows),
            new("خلاصه خرید", new[]
            {
                "تعداد فاکتور خرید", "مجموع بهای خرید (تومان)", "مجموع مالیات خرید (تومان)", "مجموع هزینه حمل (تومان)",
            }, summaryRows),
        });
    }

    private async Task<(string, IReadOnlyCollection<ExcelSheet>)> BuildSuppliersAsync(
        CancellationToken cancellationToken)
    {
        var rows = new SheetAccumulator();

        await foreach (var supplier in _dbContext.Suppliers
            .AsNoTracking()
            .Include(supplier => supplier.IngredientSuppliers)!
            .ThenInclude(link => link.Ingredient)
            .AsAsyncEnumerable()
            .WithCancellation(cancellationToken))
        {
            rows.Add(new List<object?>
            {
                supplier.PersianName,
                supplier.PhoneNumber,
                supplier.ContactName,
                supplier.DisplayOrder,
                supplier.IsActive ? "فعال" : "غیرفعال",
                supplier.IngredientSuppliers.Count,
                string.Join(" | ", supplier.IngredientSuppliers.Select(link => link.Ingredient.PersianName)),
                supplier.Notes,
            });
        }

        return ("تامین‌کنندگان", new List<ExcelSheet>
        {
            new("تامین‌کنندگان", new[]
            {
                "نام", "شماره تماس", "نام رابط", "ترتیب نمایش", "وضعیت",
                "تعداد پیمانه‌ها", "پیمانه‌ها", "یادداشت",
            }, rows.Rows),
        });
    }

    private async Task<(string, IReadOnlyCollection<ExcelSheet>)> BuildDeliveryMethodsAsync(
        CancellationToken cancellationToken)
    {
        var rows = new SheetAccumulator();

        await foreach (var method in _dbContext.DeliveryMethods
            .AsNoTracking()
            .Include(method => method.Packagings)!
            .ThenInclude(packaging => packaging.Ingredient)
            .AsAsyncEnumerable()
            .WithCancellation(cancellationToken))
        {
            rows.Add(new List<object?>
            {
                method.PersianName,
                method.EnglishName,
                method.DeliveryFee,
                method.DisplayOrder,
                method.IsActive ? "فعال" : "غیرفعال",
                method.Packagings.Count,
                string.Join(" | ", method.Packagings.Select(packaging =>
                    $"{packaging.Ingredient.PersianName} × {packaging.QuantityPerOrder}")),
                method.Description,
            });
        }

        return ("روش‌های تحویل و بسته‌بندی", new List<ExcelSheet>
        {
            new("روش تحویل", new[]
            {
                "نام", "نام انگلیسی", "هزینه تحویل (تومان)", "ترتیب نمایش", "وضعیت",
                "تعداد اقلام بسته‌بندی", "اقلام بسته‌بندی", "توضیحات",
            }, rows.Rows),
        });
    }

    private async Task<(string, IReadOnlyCollection<ExcelSheet>)> BuildAccountingAsync(
        DateOnly? from, DateOnly? to, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.DateTime);
        var journalFrom = from ?? today.AddYears(-1);
        var journalTo = to ?? today;

        var journalRows = new SheetAccumulator();

        await foreach (var entry in _dbContext.JournalEntries
            .AsNoTracking()
            .Include(entry => entry.Lines)!
            .ThenInclude(line => line.Account)
            .Include(entry => entry.Lines)!
            .ThenInclude(line => line.Partner)
            .Where(entry => entry.EntryDate >= journalFrom && entry.EntryDate <= journalTo)
            .OrderBy(entry => entry.EntryDate)
            .ThenBy(entry => entry.CreatedAt)
            .AsAsyncEnumerable()
            .WithCancellation(cancellationToken))
        {
            foreach (var line in entry.Lines)
            {
                journalRows.Add(new List<object?>
                {
                    Shamsi(entry.EntryDate),
                    entry.ReferenceNumber,
                    entry.Description,
                    entry.SourceType.ToString(),
                    line.Account.Code,
                    line.Account.NamePersian,
                    line.Debit,
                    line.Credit,
                    line.Partner != null ? line.Partner.Name : string.Empty,
                    line.Description ?? string.Empty,
                });
            }
        }

        var trialBalance = await _sender.Send(new GetTrialBalanceQuery(journalTo), cancellationToken);
        var incomeStatement = await _sender.Send(new GetIncomeStatementQuery(journalFrom, journalTo), cancellationToken);
        var balanceSheet = await _sender.Send(new GetBalanceSheetQuery(journalTo), cancellationToken);

        var trialRows = new List<IReadOnlyList<object?>>();
        foreach (var line in trialBalance.Lines)
        {
            trialRows.Add(new List<object?>
            {
                line.AccountCode,
                line.AccountNamePersian,
                AccountTypeFa(line.AccountType),
                line.Debit,
                line.Credit,
            });
        }

        trialRows.Add(new List<object?>
            { string.Empty, "جمع", string.Empty, trialBalance.TotalDebit, trialBalance.TotalCredit });

        var incomeRows = new List<IReadOnlyList<object?>>();
        incomeRows.Add(new List<object?> { "کد حساب", "نام حساب", "مبلغ درآمد (تومان)" });
        foreach (var line in incomeStatement.RevenueLines)
        {
            incomeRows.Add(new List<object?> { line.AccountCode, line.AccountNamePersian, line.Amount });
        }

        incomeRows.Add(new List<object?> { string.Empty, "جمع درآمد", incomeStatement.TotalRevenue });
        incomeRows.Add(new List<object?> { "کد حساب", "نام حساب", "مبلغ هزینه (تومان)" });
        foreach (var line in incomeStatement.ExpenseLines)
        {
            incomeRows.Add(new List<object?> { line.AccountCode, line.AccountNamePersian, line.Amount });
        }

        incomeRows.Add(new List<object?> { string.Empty, "جمع هزینه", incomeStatement.TotalExpenses });
        incomeRows.Add(new List<object?> { string.Empty, "سود خالص (تومان)", incomeStatement.NetProfit });

        var balanceRows = new List<IReadOnlyList<object?>>();
        balanceRows.Add(new List<object?> { "کد حساب", "نام حساب", "دارایی (تومان)" });
        balanceRows.AddRange(balanceSheet.Assets.Select(line =>
            (IReadOnlyList<object?>)new List<object?> { line.AccountCode, line.AccountNamePersian, line.Amount }));
        balanceRows.Add(new List<object?> { string.Empty, "جمع دارایی", balanceSheet.TotalAssets });
        balanceRows.Add(new List<object?> { "کد حساب", "نام حساب", "بدهی (تومان)" });
        balanceRows.AddRange(balanceSheet.Liabilities.Select(line =>
            (IReadOnlyList<object?>)new List<object?> { line.AccountCode, line.AccountNamePersian, line.Amount }));
        balanceRows.Add(new List<object?> { string.Empty, "جمع بدهی", balanceSheet.TotalLiabilities });
        balanceRows.Add(new List<object?> { "کد حساب", "نام حساب", "سرمایه (تومان)" });
        balanceRows.AddRange(balanceSheet.Equity.Select(line =>
            (IReadOnlyList<object?>)new List<object?> { line.AccountCode, line.AccountNamePersian, line.Amount }));
        balanceRows.Add(new List<object?> { string.Empty, "جمع سرمایه", balanceSheet.TotalEquity });
        balanceRows.Add(new List<object?> { "کد حساب", "نام حساب", "مبلغ (تومان)" });

        var title = $"گزارش‌های مالی (دفتر، تراز آزمایشی، سود و زیان، ترازنامه){PeriodSuffix(journalFrom, journalTo)}";
        return (title, new List<ExcelSheet>
        {
            new("دفتر روزنامه", new[]
            {
                "تاریخ", "شماره سند", "شرح", "منبع", "کد حساب", "نام حساب",
                "بدهکار (تومان)", "بستانکار (تومان)", "شریک", "توضیح ردیف",
            }, journalRows.Rows),
            new("تراز آزمایشی", new[]
            {
                "کد حساب", "نام حساب", "نوع حساب", "بدهکار (تومان)", "بستانکار (تومان)",
            }, trialRows),
            new("سود و زیان", new[]
            {
                "کد حساب", "نام حساب", "مبلغ (تومان)",
            }, incomeRows),
            new("ترازنامه", new[]
            {
                "کد حساب", "نام حساب", "مبلغ (تومان)",
            }, balanceRows),
        });
    }

    private static string StatusFa(string status) => status switch
    {
        "Held" => "نگه‌داشته",
        "PendingPayment" => "در انتظار پرداخت",
        "PartiallyPaid" => "پرداخت جزئی",
        "Paid" => "پرداخت‌شده",
        "Failed" => "ناموفق",
        "Cancelled" => "لغوشده",
        _ => status,
    };

    private static string FulfillmentFa(string status) => status switch
    {
        "None" => "بدون آماده‌سازی",
        "Registered" => "ثبت‌شده",
        "Preparing" => "در حال آماده‌سازی",
        "Ready" => "آماده",
        "Completed" => "تحویل شده",
        _ => status,
    };

    private static string ServiceTypeFa(string? serviceType) => serviceType switch
    {
        "DineIn" => "حضوری (سالن)",
        "TakeAway" => "بیرون‌بر",
        null => string.Empty,
        _ => serviceType,
    };

    private static string ChannelFa(string channel) => channel switch
    {
        "pos" => "صندوق فروش",
        "online" => "سفارش آنلاین",
        "admin" => "ویرایش ادمین",
        _ => channel,
    };

    private static string IngredientKindFa(string kind) => kind switch
    {
        "packaging" => "بسته‌بندی",
        "ingredient" => "ماده اولیه",
        _ => kind,
    };

    private static string MovementFa(string movementType) => movementType switch
    {
        "Restock" => "ورود موجودی",
        "SaleDeduction" => "کسر فروش",
        "SaleReversal" => "بازگردانی فروش",
        "PackagingConsumption" => "مصرف بسته‌بندی",
        "Production" => "تولید",
        "Adjustment" => "اصلاح موجودی",
        "Waste" => "ضایعات",
        _ => movementType,
    };

    private static string AccountTypeFa(string accountType) => accountType switch
    {
        "Asset" => "دارایی",
        "Liability" => "بدهی",
        "Equity" => "سرمایه",
        "Revenue" => "درآمد",
        "Expense" => "هزینه",
        _ => accountType,
    };
}
