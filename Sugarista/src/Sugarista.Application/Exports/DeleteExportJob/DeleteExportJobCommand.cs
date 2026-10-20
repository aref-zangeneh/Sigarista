using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;

namespace Sugarista.Application.Exports.DeleteExportJob;

public record DeleteExportJobCommand(Guid JobId) : IRequest;

public class DeleteExportJobCommandHandler : IRequestHandler<DeleteExportJobCommand>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IExportFileStore _fileStore;

    public DeleteExportJobCommandHandler(IApplicationDbContext dbContext, IExportFileStore fileStore)
    {
        _dbContext = dbContext;
        _fileStore = fileStore;
    }

    public async Task Handle(
        DeleteExportJobCommand request,
        CancellationToken cancellationToken)
    {
        var job = await _dbContext.ExportJobs
            .FirstOrDefaultAsync(job => job.Id == request.JobId, cancellationToken)
            ?? throw new KeyNotFoundException("Export job was not found.");

        if (!string.IsNullOrEmpty(job.StoragePath))
        {
            _fileStore.Delete(job.StoragePath);
        }

        _dbContext.ExportJobs.Remove(job);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
