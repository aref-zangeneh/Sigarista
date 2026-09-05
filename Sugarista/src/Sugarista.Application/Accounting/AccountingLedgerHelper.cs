using Sugarista.Domain.Constants;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Accounting;

internal static class AccountingLedgerHelper
{
    public static decimal SignedBalance(AccountType accountType, decimal totalDebit, decimal totalCredit) =>
        accountType is AccountType.Asset or AccountType.Expense
            ? totalDebit - totalCredit
            : totalCredit - totalDebit;

    public static async Task<IReadOnlyDictionary<Guid, (Account Account, decimal Balance)>> GetAccountBalancesAsync(
        IApplicationDbContext dbContext,
        DateOnly? asOfDate,
        CancellationToken cancellationToken)
    {
        var accounts = await dbContext.Accounts
            .AsNoTracking()
            .Where(account => account.IsActive)
            .OrderBy(account => account.Code)
            .ToListAsync(cancellationToken);

        var lineQuery = dbContext.JournalLines
            .AsNoTracking()
            .Where(line => line.JournalEntry.IsPosted);

        if (asOfDate.HasValue)
        {
            lineQuery = lineQuery.Where(line => line.JournalEntry.EntryDate <= asOfDate.Value);
        }

        var totals = await lineQuery
            .GroupBy(line => line.AccountId)
            .Select(group => new
            {
                AccountId = group.Key,
                TotalDebit = group.Sum(line => line.Debit),
                TotalCredit = group.Sum(line => line.Credit),
            })
            .ToListAsync(cancellationToken);

        var totalsByAccount = totals.ToDictionary(entry => entry.AccountId);

        return accounts.ToDictionary(
            account => account.Id,
            account =>
            {
                totalsByAccount.TryGetValue(account.Id, out var total);
                var balance = SignedBalance(account.Type, total?.TotalDebit ?? 0, total?.TotalCredit ?? 0);
                return (account, balance);
            });
    }

    public static async Task<decimal> GetAccountBalanceAsync(
        IApplicationDbContext dbContext,
        string accountCode,
        DateOnly? asOfDate,
        CancellationToken cancellationToken)
    {
        var account = await dbContext.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(entry => entry.Code == accountCode, cancellationToken);

        if (account is null)
        {
            return 0;
        }

        var balances = await GetAccountBalancesAsync(dbContext, asOfDate, cancellationToken);
        return balances.TryGetValue(account.Id, out var balance) ? balance.Balance : 0;
    }

    public static async Task<(decimal Revenue, decimal Expenses, decimal NetProfit)> GetPeriodProfitAsync(
        IApplicationDbContext dbContext,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken)
    {
        var revenueAccounts = await dbContext.Accounts
            .AsNoTracking()
            .Where(account => account.Type == AccountType.Revenue)
            .Select(account => account.Id)
            .ToListAsync(cancellationToken);

        var expenseAccounts = await dbContext.Accounts
            .AsNoTracking()
            .Where(account => account.Type == AccountType.Expense)
            .Select(account => account.Id)
            .ToListAsync(cancellationToken);

        var lines = await dbContext.JournalLines
            .AsNoTracking()
            .Where(line =>
                line.JournalEntry.IsPosted
                && line.JournalEntry.EntryDate >= fromDate
                && line.JournalEntry.EntryDate <= toDate
                && (revenueAccounts.Contains(line.AccountId) || expenseAccounts.Contains(line.AccountId)))
            .Select(line => new { line.AccountId, line.Debit, line.Credit })
            .ToListAsync(cancellationToken);

        var revenue = lines
            .Where(line => revenueAccounts.Contains(line.AccountId))
            .Sum(line => line.Credit - line.Debit);

        var expenses = lines
            .Where(line => expenseAccounts.Contains(line.AccountId))
            .Sum(line => line.Debit - line.Credit);

        return (revenue, expenses, revenue - expenses);
    }

    public static async Task<decimal> GetPartnerEquityAsync(
        IApplicationDbContext dbContext,
        Guid partnerId,
        CancellationToken cancellationToken)
    {
        var capitalAccount = await dbContext.Accounts
            .AsNoTracking()
            .FirstAsync(account => account.Code == AccountingAccountCodes.PartnerCapital, cancellationToken);

        var drawingsAccount = await dbContext.Accounts
            .AsNoTracking()
            .FirstAsync(account => account.Code == AccountingAccountCodes.PartnerDrawings, cancellationToken);

        var capitalLines = await dbContext.JournalLines
            .AsNoTracking()
            .Where(line => line.PartnerId == partnerId && line.AccountId == capitalAccount.Id)
            .Select(line => new { line.Debit, line.Credit })
            .ToListAsync(cancellationToken);

        var drawingLines = await dbContext.JournalLines
            .AsNoTracking()
            .Where(line => line.PartnerId == partnerId && line.AccountId == drawingsAccount.Id)
            .Select(line => new { line.Debit, line.Credit })
            .ToListAsync(cancellationToken);

        var capitalBalance = capitalLines.Sum(line => line.Credit - line.Debit);
        var drawingsBalance = drawingLines.Sum(line => line.Debit - line.Credit);

        return capitalBalance - drawingsBalance;
    }
}
