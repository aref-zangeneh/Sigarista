namespace Sugarista.Application.Accounting.Queries.ListPartners;

public record ListPartnersQuery(bool ActiveOnly = false) : IRequest<IReadOnlyCollection<PartnerDto>>;

public record PartnerDto(
    Guid Id,
    string Name,
    decimal OwnershipPercentage,
    string? Email,
    string? Phone,
    string? Notes,
    bool IsActive,
    decimal EquityBalance,
    decimal TotalContributions,
    decimal TotalWithdrawals);

public class ListPartnersQueryHandler : IRequestHandler<ListPartnersQuery, IReadOnlyCollection<PartnerDto>>
{
    private readonly IApplicationDbContext _dbContext;

    public ListPartnersQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<PartnerDto>> Handle(ListPartnersQuery request, CancellationToken cancellationToken)
    {
        var query = _dbContext.Partners.AsNoTracking().AsQueryable();
        if (request.ActiveOnly)
        {
            query = query.Where(partner => partner.IsActive);
        }

        var partners = await query.OrderBy(partner => partner.Name).ToListAsync(cancellationToken);
        var results = new List<PartnerDto>();

        foreach (var partner in partners)
        {
            var equity = await Accounting.AccountingLedgerHelper.GetPartnerEquityAsync(
                _dbContext, partner.Id, cancellationToken);

            var transactions = await _dbContext.PartnerCapitalTransactions
                .AsNoTracking()
                .Where(transaction => transaction.PartnerId == partner.Id)
                .ToListAsync(cancellationToken);

            var contributions = transactions
                .Where(transaction => transaction.Type == Domain.Entities.PartnerCapitalTransactionType.Contribution)
                .Sum(transaction => transaction.Amount);
            var withdrawals = transactions
                .Where(transaction => transaction.Type == Domain.Entities.PartnerCapitalTransactionType.Withdrawal)
                .Sum(transaction => transaction.Amount);

            results.Add(new PartnerDto(
                partner.Id,
                partner.Name,
                partner.OwnershipPercentage,
                partner.Email,
                partner.Phone,
                partner.Notes,
                partner.IsActive,
                equity,
                contributions,
                withdrawals));
        }

        return results;
    }
}
