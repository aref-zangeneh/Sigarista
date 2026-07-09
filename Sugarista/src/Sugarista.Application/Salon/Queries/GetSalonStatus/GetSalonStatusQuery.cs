using Microsoft.EntityFrameworkCore;

namespace Sugarista.Application.Salon.Queries.GetSalonStatus;

public record GetSalonStatusQuery : IRequest<SalonStatusDto>;

public record SalonStatusDto(
    DateTimeOffset Timestamp,
    int TotalTables,
    int OccupiedTables,
    int FreeTables,
    int CurrentCustomers,
    IReadOnlyCollection<SalonTableStatusDto> Tables);

public record SalonTableStatusDto(
    Guid Id,
    string Name,
    int Capacity,
    int DisplayOrder,
    bool IsActive,
    bool IsOccupied,
    Guid? ActiveSessionId,
    int PeopleCount,
    DateTimeOffset? StartedAt,
    int DurationMinutes,
    string? Note);

public class GetSalonStatusQueryHandler : IRequestHandler<GetSalonStatusQuery, SalonStatusDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IDateTime _dateTime;

    public GetSalonStatusQueryHandler(IApplicationDbContext dbContext, IDateTime dateTime)
    {
        _dbContext = dbContext;
        _dateTime = dateTime;
    }

    public async Task<SalonStatusDto> Handle(GetSalonStatusQuery request, CancellationToken cancellationToken)
    {
        var now = _dateTime.UtcNow;
        var tables = await _dbContext.SalonTables
            .AsNoTracking()
            .OrderBy(table => table.DisplayOrder)
            .ThenBy(table => table.Name)
            .Select(table => new
            {
                table.Id,
                table.Name,
                table.Capacity,
                table.DisplayOrder,
                table.IsActive,
                ActiveSession = table.Sessions
                    .Where(session => session.EndedAt == null)
                    .OrderByDescending(session => session.StartedAt)
                    .Select(session => new
                    {
                        session.Id,
                        session.PeopleCount,
                        session.StartedAt,
                        session.Note
                    })
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        var tableStatuses = tables
            .Select(table =>
            {
                var durationMinutes = table.ActiveSession is null
                    ? 0
                    : Math.Max(0, (int)Math.Floor((now - table.ActiveSession.StartedAt).TotalMinutes));

                return new SalonTableStatusDto(
                    table.Id,
                    table.Name,
                    table.Capacity,
                    table.DisplayOrder,
                    table.IsActive,
                    table.ActiveSession is not null,
                    table.ActiveSession?.Id,
                    table.ActiveSession?.PeopleCount ?? 0,
                    table.ActiveSession?.StartedAt,
                    durationMinutes,
                    table.ActiveSession?.Note);
            })
            .ToList();

        var activeTables = tableStatuses.Where(table => table.IsActive).ToList();
        var occupiedTables = activeTables.Count(table => table.IsOccupied);

        return new SalonStatusDto(
            now,
            activeTables.Count,
            occupiedTables,
            activeTables.Count - occupiedTables,
            activeTables.Sum(table => table.PeopleCount),
            tableStatuses);
    }
}
