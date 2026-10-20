using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Extensions;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Exports.GetExportJobs;

public record GetExportJobsQuery(int Page = 1, int PageSize = 20)
    : IRequest<PaginatedList<Sugarista.Application.Exports.CreateExportJob.ExportJobDto>>;

public class GetExportJobsQueryHandler : IRequestHandler<GetExportJobsQuery, PaginatedList<Sugarista.Application.Exports.CreateExportJob.ExportJobDto>>
{
    private readonly IApplicationDbContext _dbContext;

    public GetExportJobsQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PaginatedList<Sugarista.Application.Exports.CreateExportJob.ExportJobDto>> Handle(
        GetExportJobsQuery,
        CancellationToken cancellationToken)
    {
        return await _dbContext.ExportJobs
            .AsNoTracking()
            .OrderByDescending(job => job.CreatedAt)
            .Select(job => new Sugarista.Application.Exports.CreateExportJob.ExportJobDto(
                job.Id,
                job.Kind,
                job.FromDate,
                job.ToDate,
                job.Status.ToString(),
                job.FileName ?? string.Empty,
                job.ErrorMessage,
                job.RowsCount,
                job.FileSizeBytes,
                job.RequestedBy,
                job.StartedAt,
                job.CompletedAt,
                job.CreatedAt))
            .ToPaginatedListAsync(new PaginationQuery(), cancellationToken);
    }
}
