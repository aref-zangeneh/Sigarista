using Microsoft.EntityFrameworkCore;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Hr.Glossary.GetHrGlossary;

public record HrGlossaryTermDto(string Key, string Definition);

public record GetHrGlossaryQuery : IRequest<IReadOnlyList<HrGlossaryTermDto>>;

/// <summary>
/// Returns every HR term definition — missing terms are seeded from the
/// default Persian dictionary on the first read, then stay fully editable.
/// </summary>
public class GetHrGlossaryQueryHandler : IRequestHandler<GetHrGlossaryQuery, IReadOnlyList<HrGlossaryTermDto>>
{
    private readonly IApplicationDbContext _dbContext;

    public GetHrGlossaryQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<HrGlossaryTermDto>> Handle(
        GetHrGlossaryQuery request,
        CancellationToken cancellationToken)
    {
        var existing = await _dbContext.HrGlossaryTerms
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var byKey = existing.ToDictionary(term => term.Key, term => term);

        var added = false;
        foreach (var (key, definition) in Sugarista.Application.Hr.HrGlossaryDefaults.DefaultDefinitions)
        {
            if (byKey.ContainsKey(key))
            {
                continue;
            }

            var term = new HrGlossaryTerm(key, definition);
            _dbContext.HrGlossaryTerms.Add(term);
            byKey[key] = term;
            added = true;
        }

        if (added)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return byKey.Values
            .OrderBy(term => term.Key)
            .Select(term => new HrGlossaryTermDto(term.Key, term.Definition))
            .ToList();
    }
}
