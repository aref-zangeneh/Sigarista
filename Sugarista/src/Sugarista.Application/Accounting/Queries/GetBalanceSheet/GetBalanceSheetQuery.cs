using Sugarista.Domain.Entities;

namespace Sugarista.Application.Accounting.Queries.GetBalanceSheet;

public record GetBalanceSheetQuery(DateOnly? AsOfDate = null) : IRequest<BalanceSheetDto>;

public record BalanceSheetDto(
    DateOnly AsOfDate,
    IReadOnlyCollection<BalanceSheetLineDto> Assets,
    IReadOnlyCollection<BalanceSheetLineDto> Liabilities,
    IReadOnlyCollection<BalanceSheetLineDto> Equity,
    decimal TotalAssets,
    decimal TotalLiabilities,
    decimal TotalEquity);

public record BalanceSheetLineDto(string AccountCode, string AccountNamePersian, decimal Amount);

public class GetBalanceSheetQueryHandler : IRequestHandler<GetBalanceSheetQuery, BalanceSheetDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IDateTime _dateTime;

    public GetBalanceSheetQueryHandler(IApplicationDbContext dbContext, IDateTime dateTime)
    {
        _dbContext = dbContext;
        _dateTime = dateTime;
    }

    public async Task<BalanceSheetDto> Handle(GetBalanceSheetQuery request, CancellationToken cancellationToken)
    {
        var asOfDate = request.AsOfDate ?? DateOnly.FromDateTime(_dateTime.UtcNow.DateTime);
        var balances = await AccountingLedgerHelper.GetAccountBalancesAsync(_dbContext, asOfDate, cancellationToken);

        var assets = new List<BalanceSheetLineDto>();
        var liabilities = new List<BalanceSheetLineDto>();
        var equity = new List<BalanceSheetLineDto>();

        foreach (var (account, balance) in balances.Values)
        {
            if (balance == 0)
            {
                continue;
            }

            var line = new BalanceSheetLineDto(account.Code, account.NamePersian, balance);
            switch (account.Type)
            {
                case AccountType.Asset:
                    assets.Add(line);
                    break;
                case AccountType.Liability:
                    liabilities.Add(line);
                    break;
                case AccountType.Equity:
                    equity.Add(line);
                    break;
            }
        }

        var totalAssets = assets.Sum(line => line.Amount);
        var totalLiabilities = liabilities.Sum(line => line.Amount);
        var totalEquity = equity.Sum(line => line.Amount);

        return new BalanceSheetDto(
            asOfDate,
            assets.OrderBy(line => line.AccountCode).ToArray(),
            liabilities.OrderBy(line => line.AccountCode).ToArray(),
            equity.OrderBy(line => line.AccountCode).ToArray(),
            totalAssets,
            totalLiabilities,
            totalEquity);
    }
}
