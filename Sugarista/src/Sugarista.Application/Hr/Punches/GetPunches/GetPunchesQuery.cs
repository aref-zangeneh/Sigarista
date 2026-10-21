using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Hr.Punches.PunchIn;

namespace Sugarista.Application.Hr.Punches.GetPunches;

public record GetPunchesQuery(
    Guid? EmployeeId = null,
    DateOnly? From = null,
    DateOnly? To = null,
    int Page = 1,
    int PageSize = 50) : IRequest<PaginatedList<PunchDto>>;

public class GetPunchesQueryHandler : IRequestHandler<GetPunchesQuery, PaginatedList<PunchDto>>
{
    private readonly IApplicationDbContext _dbContext;

    public GetPunchesQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PaginatedList<PunchDto>> Handle(
        GetPunchesQuery request,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.PunchAttendances
            .AsNoTracking()
            .Include(attendance => attendance.Employee)
            .AsQueryable();        if (request.EmployeeId.HasValue)
        {
            query = query.Where(attendance => attendance.EmployeeId == request.EmployeeId.Value);
        }

        if (request.From.HasValue)
        {
            query = query.Where(attendance => attendance.WorkDate >= request.From.Value);
        }

        if (request.To.HasValue)
        {
            query = query.Where(attendance => attendance.WorkDate <= request.To.Value);
        }

        var list = await query
            .OrderByDescending(attendance => attendance.WorkDate)
            .ThenByDescending(attendance => attendance.PunchInAt)
            .ToListAsync(cancellationToken);

        var pagination = new PaginationQuery(request.Page, request.PageSize);
        var paged = list
            .Skip(pagination.Skip)
            .Take(pagination.NormalizedPageSize)
            .Select(attendance => PunchMapper.ToDto(attendance, attendance.Employee!.PersianName))
            .ToList();

        return new PaginatedList<PunchDto>(
            paged,
            list.Count,
            pagination.NormalizedPage,
            pagination.NormalizedPageSize);
    }
}
