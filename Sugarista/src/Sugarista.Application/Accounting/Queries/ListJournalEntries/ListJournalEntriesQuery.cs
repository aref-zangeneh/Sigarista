namespace Sugarista.Application.Accounting.Queries.ListJournalEntries;

public record ListJournalEntriesQuery(
    DateOnly? FromDate = null,
    DateOnly? ToDate = null,
    int Limit = 100) : IRequest<IReadOnlyCollection<JournalEntryDto>>;

public record JournalEntryDto(
    Guid Id,
    string ReferenceNumber,
    DateOnly EntryDate,
    string SourceType,
    string Description,
    decimal TotalDebit,
    IReadOnlyCollection<JournalLineDto> Lines);

public record JournalLineDto(
    Guid Id,
    string AccountCode,
    string AccountNamePersian,
    decimal Debit,
    decimal Credit,
    string? PartnerName,
    string? Description);

public class ListJournalEntriesQueryHandler : IRequestHandler<ListJournalEntriesQuery, IReadOnlyCollection<JournalEntryDto>>
{
    private readonly IApplicationDbContext _dbContext;

    public ListJournalEntriesQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<JournalEntryDto>> Handle(
        ListJournalEntriesQuery request,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.JournalEntries
            .AsNoTracking()
            .Include(entry => entry.Lines)
            .ThenInclude(line => line.Account)
            .Include(entry => entry.Lines)
            .ThenInclude(line => line.Partner)
            .AsQueryable();

        if (request.FromDate.HasValue)
        {
            query = query.Where(entry => entry.EntryDate >= request.FromDate.Value);
        }

        if (request.ToDate.HasValue)
        {
            query = query.Where(entry => entry.EntryDate <= request.ToDate.Value);
        }

        var entries = await query
            .OrderByDescending(entry => entry.EntryDate)
            .ThenByDescending(entry => entry.ReferenceNumber)
            .Take(Math.Clamp(request.Limit, 1, 500))
            .ToListAsync(cancellationToken);

        return entries.Select(entry => new JournalEntryDto(
            entry.Id,
            entry.ReferenceNumber,
            entry.EntryDate,
            entry.SourceType.ToString(),
            entry.Description,
            entry.Lines.Sum(line => line.Debit),
            entry.Lines.Select(line => new JournalLineDto(
                line.Id,
                line.Account.Code,
                line.Account.NamePersian,
                line.Debit,
                line.Credit,
                line.Partner?.Name,
                line.Description)).ToArray())).ToArray();
    }
}
