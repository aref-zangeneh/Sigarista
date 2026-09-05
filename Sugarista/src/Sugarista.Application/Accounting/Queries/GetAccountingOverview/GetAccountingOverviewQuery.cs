using Sugarista.Application.Accounting.Services;
using Sugarista.Domain.Constants;

namespace Sugarista.Application.Accounting.Queries.GetAccountingOverview;

public record GetAccountingOverviewQuery(DateOnly? FromDate = null, DateOnly? ToDate = null)
    : IRequest<AccountingOverviewDto>;

public record AccountingOverviewDto(
    decimal CashBalance,
    decimal CardReceivable,
    decimal AccountsPayable,
    decimal InventoryValue,
    decimal PeriodRevenue,
    decimal PeriodExpenses,
    decimal PeriodNetProfit,
    decimal TotalPartnerCapital,
    int JournalEntryCount,
    int ActivePartnerCount,
    IReadOnlyCollection<PartnerSummaryDto> Partners);

public record PartnerSummaryDto(
    Guid Id,
    string Name,
    decimal OwnershipPercentage,
    decimal ContributedCapital,
    decimal Withdrawals,
    decimal EquityBalance,
    decimal ExpectedProfitShare,
    bool IsActive);

public class GetAccountingOverviewQueryHandler : IRequestHandler<GetAccountingOverviewQuery, AccountingOverviewDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IAccountingService _accountingService;
    private readonly IDateTime _dateTime;

    public GetAccountingOverviewQueryHandler(
        IApplicationDbContext dbContext,
        IAccountingService accountingService,
        IDateTime dateTime)
    {
        _dbContext = dbContext;
        _accountingService = accountingService;
        _dateTime = dateTime;
    }

    public async Task<AccountingOverviewDto> Handle(GetAccountingOverviewQuery request, CancellationToken cancellationToken)
    {
        await _accountingService.EnsureAccountsSeededAsync(cancellationToken);

        var today = DateOnly.FromDateTime(_dateTime.UtcNow.DateTime);
        var fromDate = request.FromDate ?? today.AddDays(-30);
        var toDate = request.ToDate ?? today;

        var cashBalance = await AccountingLedgerHelper.GetAccountBalanceAsync(
            _dbContext, AccountingAccountCodes.Cash, toDate, cancellationToken);
        var cardBalance = await AccountingLedgerHelper.GetAccountBalanceAsync(
            _dbContext, AccountingAccountCodes.CardReceivable, toDate, cancellationToken);
        var payableBalance = await AccountingLedgerHelper.GetAccountBalanceAsync(
            _dbContext, AccountingAccountCodes.AccountsPayable, toDate, cancellationToken);
        var inventoryBalance = await AccountingLedgerHelper.GetAccountBalanceAsync(
            _dbContext, AccountingAccountCodes.Inventory, toDate, cancellationToken);
        var partnerCapitalBalance = await AccountingLedgerHelper.GetAccountBalanceAsync(
            _dbContext, AccountingAccountCodes.PartnerCapital, toDate, cancellationToken);

        var (revenue, expenses, netProfit) = await AccountingLedgerHelper.GetPeriodProfitAsync(
            _dbContext, fromDate, toDate, cancellationToken);

        var partners = await _dbContext.Partners
            .AsNoTracking()
            .OrderBy(partner => partner.Name)
            .ToListAsync(cancellationToken);

        var capitalAccountId = await _dbContext.Accounts
            .AsNoTracking()
            .Where(account => account.Code == AccountingAccountCodes.PartnerCapital)
            .Select(account => account.Id)
            .FirstAsync(cancellationToken);

        var drawingsAccountId = await _dbContext.Accounts
            .AsNoTracking()
            .Where(account => account.Code == AccountingAccountCodes.PartnerDrawings)
            .Select(account => account.Id)
            .FirstAsync(cancellationToken);

        var partnerLines = await _dbContext.JournalLines
            .AsNoTracking()
            .Where(line => line.PartnerId != null
                && (line.AccountId == capitalAccountId || line.AccountId == drawingsAccountId))
            .Select(line => new { line.PartnerId, line.AccountId, line.Debit, line.Credit })
            .ToListAsync(cancellationToken);

        var partnerSummaries = new List<PartnerSummaryDto>();
        foreach (var partner in partners)
        {
            var lines = partnerLines.Where(line => line.PartnerId == partner.Id).ToList();
            var contributed = lines
                .Where(line => line.AccountId == capitalAccountId)
                .Sum(line => line.Credit);
            var withdrawals = lines
                .Where(line => line.AccountId == drawingsAccountId)
                .Sum(line => line.Debit);
            var equity = await AccountingLedgerHelper.GetPartnerEquityAsync(_dbContext, partner.Id, cancellationToken);
            var expectedShare = netProfit > 0 ? Math.Round(netProfit * partner.OwnershipPercentage / 100m, 2) : 0;

            partnerSummaries.Add(new PartnerSummaryDto(
                partner.Id,
                partner.Name,
                partner.OwnershipPercentage,
                contributed,
                withdrawals,
                equity,
                expectedShare,
                partner.IsActive));
        }

        var journalCount = await _dbContext.JournalEntries
            .CountAsync(entry => entry.EntryDate >= fromDate && entry.EntryDate <= toDate, cancellationToken);

        return new AccountingOverviewDto(
            cashBalance,
            cardBalance,
            payableBalance,
            inventoryBalance,
            revenue,
            expenses,
            netProfit,
            partnerCapitalBalance,
            journalCount,
            partners.Count(partner => partner.IsActive),
            partnerSummaries);
    }
}
