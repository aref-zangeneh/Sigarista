using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Domain.Constants;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Accounting.Services;

public interface IAccountingService
{
    Task EnsureAccountsSeededAsync(CancellationToken cancellationToken = default);

    Task PostOrderPaymentAsync(Order order, CancellationToken cancellationToken = default);

    Task PostInventoryPurchaseAsync(InventoryPurchase purchase, CancellationToken cancellationToken = default);

    Task<Guid> PostPartnerCapitalAsync(
        PartnerCapitalTransaction transaction,
        CancellationToken cancellationToken = default);

    Task<Guid> PostManualExpenseAsync(
        DateOnly entryDate,
        decimal amount,
        string description,
        bool paidFromCash,
        CancellationToken cancellationToken = default);

    Task<Guid> PostProfitDistributionAsync(
        ProfitDistribution distribution,
        CancellationToken cancellationToken = default);

    Task<decimal> CalculateOrderCogsAsync(Order order, CancellationToken cancellationToken = default);
}

internal sealed class AccountingService : IAccountingService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IDateTime _dateTime;

    public AccountingService(IApplicationDbContext dbContext, IDateTime dateTime)
    {
        _dbContext = dbContext;
        _dateTime = dateTime;
    }

    public async Task EnsureAccountsSeededAsync(CancellationToken cancellationToken = default)
    {
        var existingCodes = await _dbContext.Accounts
            .Select(account => account.Code)
            .ToListAsync(cancellationToken);

        foreach (var seed in AccountingAccountCodes.SeedAccounts)
        {
            if (existingCodes.Contains(seed.Code))
            {
                continue;
            }

            _dbContext.Accounts.Add(new Account(seed.Code, seed.Name, seed.NamePersian, seed.Type, isSystem: true));
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task PostOrderPaymentAsync(Order order, CancellationToken cancellationToken = default)
    {
        if (order.Status != OrderStatus.Paid)
        {
            return;
        }

        if (await AlreadyPostedAsync(JournalSourceType.OrderPayment, order.Id, cancellationToken))
        {
            return;
        }

        await EnsureAccountsSeededAsync(cancellationToken);

        var cashAccount = await GetAccountAsync(AccountingAccountCodes.Cash, cancellationToken);
        var cardAccount = await GetAccountAsync(AccountingAccountCodes.CardReceivable, cancellationToken);
        var revenueAccount = await GetAccountAsync(AccountingAccountCodes.SalesRevenue, cancellationToken);
        var cogsAccount = await GetAccountAsync(AccountingAccountCodes.CostOfGoodsSold, cancellationToken);
        var inventoryAccount = await GetAccountAsync(AccountingAccountCodes.Inventory, cancellationToken);

        var assetAccount = order.PaymentMethod == PosPaymentMethod.Cash
            ? cashAccount
            : cardAccount;
        var entryDate = DateOnly.FromDateTime((order.PaidAt ?? _dateTime.UtcNow).DateTime);
        var reference = await NextReferenceAsync(entryDate, cancellationToken);
        var entry = new JournalEntry(
            reference,
            entryDate,
            JournalSourceType.OrderPayment,
            $"Payment for order {order.TrackingCode}",
            order.Id);

        entry.AddLine(new JournalLine(entry.Id, assetAccount.Id, order.TotalAmount, 0, "Receipt of payment"));
        entry.AddLine(new JournalLine(entry.Id, revenueAccount.Id, 0, order.TotalAmount, "Sales revenue"));

        var cogs = await CalculateOrderCogsAsync(order, cancellationToken);
        if (cogs > 0)
        {
            entry.AddLine(new JournalLine(entry.Id, cogsAccount.Id, cogs, 0, "Cost of goods sold"));
            entry.AddLine(new JournalLine(entry.Id, inventoryAccount.Id, 0, cogs, "Inventory reduction"));
        }

        entry.ValidateBalanced();
        _dbContext.JournalEntries.Add(entry);
        foreach (var line in entry.Lines)
        {
            _dbContext.JournalLines.Add(line);
        }
    }

    public async Task PostInventoryPurchaseAsync(InventoryPurchase purchase, CancellationToken cancellationToken = default)
    {
        if (await AlreadyPostedAsync(JournalSourceType.InventoryPurchase, purchase.Id, cancellationToken))
        {
            return;
        }

        await EnsureAccountsSeededAsync(cancellationToken);

        var inventoryAccount = await GetAccountAsync(AccountingAccountCodes.Inventory, cancellationToken);
        var vatAccount = await GetAccountAsync(AccountingAccountCodes.InputVat, cancellationToken);
        var payableAccount = await GetAccountAsync(AccountingAccountCodes.AccountsPayable, cancellationToken);

        var inventoryAmount = purchase.TotalCost - purchase.TaxAmount;
        var entryDate = DateOnly.FromDateTime(purchase.ReceivedAt.DateTime);
        var reference = await NextReferenceAsync(entryDate, cancellationToken);
        var entry = new JournalEntry(
            reference,
            entryDate,
            JournalSourceType.InventoryPurchase,
            $"Inventory purchase #{purchase.Id.ToString()[..8]}",
            purchase.Id);

        if (inventoryAmount > 0)
        {
            entry.AddLine(new JournalLine(entry.Id, inventoryAccount.Id, inventoryAmount, 0, "Inventory asset"));
        }

        if (purchase.TaxAmount > 0)
        {
            entry.AddLine(new JournalLine(entry.Id, vatAccount.Id, purchase.TaxAmount, 0, "Input VAT"));
        }

        var creditAmount = inventoryAmount + purchase.TaxAmount;
        if (creditAmount <= 0)
        {
            return;
        }

        entry.AddLine(new JournalLine(entry.Id, payableAccount.Id, 0, creditAmount, "Accounts payable"));

        entry.ValidateBalanced();
        _dbContext.JournalEntries.Add(entry);
        foreach (var line in entry.Lines)
        {
            _dbContext.JournalLines.Add(line);
        }
    }

    public async Task<Guid> PostPartnerCapitalAsync(
        PartnerCapitalTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        await EnsureAccountsSeededAsync(cancellationToken);

        var cashAccount = await GetAccountAsync(AccountingAccountCodes.Cash, cancellationToken);
        var capitalAccount = await GetAccountAsync(AccountingAccountCodes.PartnerCapital, cancellationToken);
        var drawingsAccount = await GetAccountAsync(AccountingAccountCodes.PartnerDrawings, cancellationToken);

        var sourceType = transaction.Type == PartnerCapitalTransactionType.Contribution
            ? JournalSourceType.PartnerCapital
            : JournalSourceType.PartnerWithdrawal;

        var reference = await NextReferenceAsync(transaction.TransactionDate, cancellationToken);
        var entry = new JournalEntry(
            reference,
            transaction.TransactionDate,
            sourceType,
            transaction.Description ?? $"Partner {transaction.Type}",
            transaction.Id);

        if (transaction.Type == PartnerCapitalTransactionType.Contribution)
        {
            entry.AddLine(new JournalLine(entry.Id, cashAccount.Id, transaction.Amount, 0, "Capital contribution", transaction.PartnerId));
            entry.AddLine(new JournalLine(entry.Id, capitalAccount.Id, 0, transaction.Amount, "Partner capital", transaction.PartnerId));
        }
        else
        {
            entry.AddLine(new JournalLine(entry.Id, drawingsAccount.Id, transaction.Amount, 0, "Partner withdrawal", transaction.PartnerId));
            entry.AddLine(new JournalLine(entry.Id, cashAccount.Id, 0, transaction.Amount, "Cash paid out", transaction.PartnerId));
        }

        entry.ValidateBalanced();
        _dbContext.JournalEntries.Add(entry);
        foreach (var line in entry.Lines)
        {
            _dbContext.JournalLines.Add(line);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return entry.Id;
    }

    public async Task<Guid> PostManualExpenseAsync(
        DateOnly entryDate,
        decimal amount,
        string description,
        bool paidFromCash,
        CancellationToken cancellationToken = default)
    {
        await EnsureAccountsSeededAsync(cancellationToken);

        var expenseAccount = await GetAccountAsync(AccountingAccountCodes.OperatingExpense, cancellationToken);
        var cashAccount = await GetAccountAsync(AccountingAccountCodes.Cash, cancellationToken);
        var payableAccount = await GetAccountAsync(AccountingAccountCodes.AccountsPayable, cancellationToken);
        var creditAccount = paidFromCash ? cashAccount : payableAccount;

        var reference = await NextReferenceAsync(entryDate, cancellationToken);
        var entry = new JournalEntry(reference, entryDate, JournalSourceType.OperatingExpense, description);

        entry.AddLine(new JournalLine(entry.Id, expenseAccount.Id, amount, 0, description));
        entry.AddLine(new JournalLine(entry.Id, creditAccount.Id, 0, amount, description));

        entry.ValidateBalanced();
        _dbContext.JournalEntries.Add(entry);
        foreach (var line in entry.Lines)
        {
            _dbContext.JournalLines.Add(line);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return entry.Id;
    }

    public async Task<Guid> PostProfitDistributionAsync(
        ProfitDistribution distribution,
        CancellationToken cancellationToken = default)
    {
        await EnsureAccountsSeededAsync(cancellationToken);

        var retainedAccount = await GetAccountAsync(AccountingAccountCodes.RetainedEarnings, cancellationToken);
        var capitalAccount = await GetAccountAsync(AccountingAccountCodes.PartnerCapital, cancellationToken);

        var reference = await NextReferenceAsync(distribution.PeriodEnd, cancellationToken);
        var entry = new JournalEntry(
            reference,
            distribution.PeriodEnd,
            JournalSourceType.ProfitDistribution,
            $"Profit distribution {distribution.PeriodStart:yyyy-MM-dd} to {distribution.PeriodEnd:yyyy-MM-dd}",
            distribution.Id);

        if (distribution.TotalNetProfit >= 0)
        {
            entry.AddLine(new JournalLine(entry.Id, retainedAccount.Id, distribution.TotalNetProfit, 0, "Allocate net profit"));
            foreach (var line in distribution.Lines)
            {
                entry.AddLine(new JournalLine(
                    entry.Id,
                    capitalAccount.Id,
                    0,
                    line.AllocatedAmount,
                    "Profit share for partner",
                    line.PartnerId));
            }
        }
        else
        {
            var lossAmount = Math.Abs(distribution.TotalNetProfit);
            entry.AddLine(new JournalLine(entry.Id, retainedAccount.Id, 0, lossAmount, "Absorb net loss"));
            foreach (var line in distribution.Lines)
            {
                entry.AddLine(new JournalLine(
                    entry.Id,
                    capitalAccount.Id,
                    Math.Abs(line.AllocatedAmount),
                    0,
                    "Loss share for partner",
                    line.PartnerId));
            }
        }

        entry.ValidateBalanced();
        _dbContext.JournalEntries.Add(entry);
        foreach (var journalLine in entry.Lines)
        {
            _dbContext.JournalLines.Add(journalLine);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return entry.Id;
    }

    public async Task<decimal> CalculateOrderCogsAsync(Order order, CancellationToken cancellationToken = default)
    {
        var menuItemIds = order.Lines.Select(line => line.MenuItemId).Distinct().ToList();
        if (menuItemIds.Count == 0)
        {
            return 0;
        }

        var recipes = await _dbContext.RecipeItems
            .AsNoTracking()
            .Include(recipe => recipe.Ingredient)
            .Where(recipe => menuItemIds.Contains(recipe.MenuItemId))
            .ToListAsync(cancellationToken);

        var recipesByMenuItem = recipes.GroupBy(recipe => recipe.MenuItemId).ToDictionary(group => group.Key, group => group.ToList());

        decimal total = 0;
        foreach (var line in order.Lines)
        {
            if (!recipesByMenuItem.TryGetValue(line.MenuItemId, out var recipeLines))
            {
                continue;
            }

            var unitCost = recipeLines.Sum(recipe => recipe.Quantity * recipe.Ingredient.UnitCost);
            total += unitCost * line.Quantity;
        }

        return Math.Round(total, 2);
    }

    private async Task<bool> AlreadyPostedAsync(JournalSourceType sourceType, Guid sourceId, CancellationToken cancellationToken)
    {
        return await _dbContext.JournalEntries
            .AnyAsync(entry => entry.SourceType == sourceType && entry.SourceId == sourceId, cancellationToken);
    }

    private async Task<Account> GetAccountAsync(string code, CancellationToken cancellationToken)
    {
        return await _dbContext.Accounts
            .FirstAsync(account => account.Code == code, cancellationToken);
    }

    private async Task<string> NextReferenceAsync(DateOnly entryDate, CancellationToken cancellationToken)
    {
        var prefix = $"JE-{entryDate:yyyyMMdd}";
        var count = await _dbContext.JournalEntries
            .CountAsync(entry => entry.ReferenceNumber.StartsWith(prefix), cancellationToken);

        return $"{prefix}-{(count + 1).ToString().PadLeft(4, '0')}";
    }
}
