namespace Sugarista.Application.Accounting.Queries.GetTrialBalance;

public record GetTrialBalanceQuery(DateOnly? AsOfDate = null) : IRequest<TrialBalanceDto>;

public record TrialBalanceDto(
    DateOnly AsOfDate,
    IReadOnlyCollection<TrialBalanceLineDto> Lines,
    decimal TotalDebit,
    decimal TotalCredit);

public record TrialBalanceLineDto(
    string AccountCode,
    string AccountNamePersian,
    string AccountType,
    decimal Debit,
    decimal Credit);

public class GetTrialBalanceQueryHandler : IRequestHandler<GetTrialBalanceQuery, TrialBalanceDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IDateTime _dateTime;

    public GetTrialBalanceQueryHandler(IApplicationDbContext dbContext, IDateTime dateTime)
    {
        _dbContext = dbContext;
        _dateTime = dateTime;
    }

    public async Task<TrialBalanceDto> Handle(GetTrialBalanceQuery request, CancellationToken cancellationToken)
    {
        var asOfDate = request.AsOfDate ?? DateOnly.FromDateTime(_dateTime.UtcNow.DateTime);
        var balances = await Accounting.AccountingLedgerHelper.GetAccountBalancesAsync(
            _dbContext, asOfDate, cancellationToken);

        var lines = balances.Values
            .Where(entry => entry.Balance != 0)
            .Select(entry =>
            {
                var balance = entry.Balance;
                var isDebitNormal = entry.Account.Type is Domain.Entities.AccountType.Asset
                    or Domain.Entities.AccountType.Expense;

                decimal debit = 0;
                decimal credit = 0;
                if (isDebitNormal)
                {
                    if (balance >= 0)
                    {
                        debit = balance;
                    }
                    else
                    {
                        credit = Math.Abs(balance);
                    }
                }
                else if (balance >= 0)
                {
                    credit = balance;
                }
                else
                {
                    debit = Math.Abs(balance);
                }

                return new TrialBalanceLineDto(
                    entry.Account.Code,
                    entry.Account.NamePersian,
                    entry.Account.Type.ToString(),
                    debit,
                    credit);
            })
            .OrderBy(line => line.AccountCode)
            .ToArray();

        return new TrialBalanceDto(
            asOfDate,
            lines,
            lines.Sum(line => line.Debit),
            lines.Sum(line => line.Credit));
    }
}
