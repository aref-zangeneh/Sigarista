using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Exports.GetExportJobFile;

/// <summary>
/// Resolves the download info of a completed export job (file name + storage path).
/// The controller streams the file from storage — no buffering in memory.
/// </summary>
public record GetExportJobFileQuery(Guid JobId) : IRequest<ExportJobFileDto>;

public record ExportJobFileDto(string FileName, string StoragePath, string ContentType);

public class GetExportJobFileQueryHandler : IRequestHandler<GetExportJobFileQuery, ExportJobFileDto>
{
    private readonly IApplicationDbContext _dbContext;

    public GetExportJobFileQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ExportJobFileDto> Handle(
        GetExportJobFileQuery request,
        CancellationToken cancellationToken)
    {
        var job = await _dbContext.ExportJobs
            .AsNoTracking()
            .SingleOrDefaultAsync(job => job.Id == request.JobId, cancellationToken)
            ?? throw new KeyNotFoundException("Export job was not found.");

        if (job.Status != ExportJobStatus.Completed || string.IsNullOrEmpty(job.StoragePath))
        {
            throw new InvalidOperationException("Export job file is not ready.");
        }

        return new ExportJobFileDto(
            job.FileName ?? $"sugarista-{job.Kind}.xlsx",
            job.StoragePath,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }
}
