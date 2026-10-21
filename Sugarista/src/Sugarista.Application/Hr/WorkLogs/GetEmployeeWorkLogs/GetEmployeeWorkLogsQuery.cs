using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Extensions;
using Sugarista.Application.Hr.Employees.GetEmployeeDetail;

namespace Sugarista.Application.Hr.WorkLogs.GetEmployeeWorkLogs;

public record GetEmployeeWorkLogsQuery(
    Guid? EmployeeId = null,
    DateOnly? From = null,
    DateOnly? To = null,
    int Page = 1,
    int PageSize = 50)
    : IRequest<PaginatedList<WorkLogDto>>;

public class GetEmployeeWorkLogsQueryHandler : IRequestHandler<GetEmployeeWorkLogsQuery, PaginatedList<WorkLogDto>>
{
    private readonly IApplicationDbContext _dbContext;

    public GetEmployeeWorkLogsQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PaginatedList<WorkLogDto>> Handle(
        GetEmployeeWorkLogsQuery request,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.EmployeeWorkLogs
            .AsNoTracking()
            .AsQueryable();

        if (request.EmployeeId.HasValue)
        {
            query = query.Where(log => log.EmployeeId == request.EmployeeId);
        }

        if (request.From.HasValue)
        {
            query = query.Where(log => log.WorkDate >= request.From.Value);
        }

        if (request.To.HasValue)
        {
            query = query.Where(log => log.WorkDate <= request.To.Value);
        }

        return await query
            .OrderByDescending(log => log.WorkDate)
            .Select(log => new WorkLogDto(
                log.Id,
                log.EmployeeId,
                log.WorkDate,
                log.WorkedHours,
                log.OvertimeHours,
                log.ShiftCount,
                log.Note))
            .ToPaginatedListAsync(
                new PaginationQuery(request.Page, request.PageSize),
                cancellationToken);
    }
}
