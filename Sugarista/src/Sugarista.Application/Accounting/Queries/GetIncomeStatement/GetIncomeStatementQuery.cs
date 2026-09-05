using Sugarista.Domain.Entities;

namespace Sugarista.Application.Accounting.Queries.GetIncomeStatement;

public record GetIncomeStatementQuery(DateOnly FromDate, DateOnly ToDate) : IRequest<IncomeStatementDto>;

public record IncomeStatementDto(
    DateOnly FromDate,
    DateOnly ToDate,
    IReadOnlyCollection<StatementLineDto> RevenueLines,
    IReadOnlyCollection<StatementLineDto> ExpenseLines,
    decimal TotalRevenue,
    decimal TotalExpenses,
    decimal NetProfit);

public record StatementLineDto(string AccountCode, string AccountNamePersian, decimal Amount);

public class GetIncomeStatementQueryHandler : IRequestHandler<GetIncomeStatementQuery, IncomeStatementDto>
{
    private readonly IApplicationDbContext _dbContext;

    public GetIncomeStatementQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IncomeStatementDto> Handle(GetIncomeStatementQuery request, CancellationToken cancellationToken)
    {
        var accounts = await _dbContext.Accounts
            .AsNoTracking()
            .Where(account => account.Type == AccountType.Revenue || account.Type == AccountType.Expense)
            .ToListAsync(cancellationToken);

        var accountIds = accounts.Select(account => account.Id).ToList();

        var grouped = await _dbContext.JournalLines
            .AsNoTracking()
            .Where(line =>
                line.JournalEntry.IsPosted
                && line.JournalEntry.EntryDate >= request.FromDate
                && line.JournalEntry.EntryDate <= request.ToDate
                && accountIds.Contains(line.AccountId))
            .GroupBy(line => line.AccountId)
            .Select(group => new
            {
                AccountId = group.Key,
                TotalDebit = group.Sum(line => line.Debit),
                TotalCredit = group.Sum(line => line.Credit),
            })
            .ToListAsync(cancellationToken);

        var revenueLines = new List<StatementLineDto>();
        var expenseLines = new List<StatementLineDto>();

        foreach (var account in accounts)
        {
            var totals = grouped.FirstOrDefault(entry => entry.AccountId == account.Id);
            if (totals is null)
            {
                continue;
            }

            var amount = AccountingLedgerHelper.SignedBalance(account.Type, totals.TotalDebit, totals.TotalCredit);
            if (amount == 0)
            {
                continue;
            }

            var line = new StatementLineDto(account.Code, account.NamePersian, amount);
            if (account.Type == AccountType.Revenue)
            {
                revenueLines.Add(line);
            }
            else
            {
                expenseLines.Add(line);
            }
        }

        var totalRevenue = revenueLines.Sum(line => line.Amount);
        var totalExpenses = expenseLines.Sum(line => line.Amount);

        return new IncomeStatementDto(
            request.FromDate,
            request.ToDate,
            revenueLines.OrderBy(line => line.AccountCode).ToArray(),
            expenseLines.OrderBy(line => line.AccountCode).ToArray(),
            totalRevenue,
            totalExpenses,
            totalRevenue - totalExpenses);
    }
}
