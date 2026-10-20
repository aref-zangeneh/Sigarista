using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.Exports;
using Sugarista.Application.Exports.CreateExportJob;
using Sugarista.Domain.Entities;

namespace Sugarista.Infrastructure.Services;

/// <summary>
/// Single-worker background service that processes queued export jobs.
/// REST calls only enqueue — this runner does the heavy Excel building so
/// huge datasets can never time out an HTTP request. On startup it also
/// re-enqueues jobs that were persisted but lost their in-memory queue
/// entry (server restart), and prunes old completed job files.
/// </summary>
public class ExportJobRunnerService : BackgroundService
{
    private const int MaxRetainedJobs = 50;

    private readonly IExportJobQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ExportJobRunnerService> _logger;

    public ExportJobRunnerService(
        IExportJobQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<ExportJobRunnerService> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RecoverPersistedJobsAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var payload = await _queue.DequeueAsync(stoppingToken);
            if (payload is null)
            {
                continue;
            }

            try
            {
                await RunJobAsync(payload.JobId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning(
                    "Export runner stopping — job {JobId} stays in Processing state and will be re-queued on next startup.",
                    payload.JobId);
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Export job {JobId} failed (outer guard).", payload.JobId);
                await MarkJobFailedAsync(payload.JobId, exception.Message);
            }
        }
    }

    private async Task RunJobAsync(Guid jobId, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var fileStore = scope.ServiceProvider.GetRequiredService<IExportFileStore>();
        var excelExporter = scope.ServiceProvider.GetRequiredService<IExcelExporter>();
        var dataBuilder = scope.ServiceProvider.GetRequiredService<ExportDataBuilder>();

        try
        {
            var job = await dbContext.ExportJobs
                .FirstOrDefaultAsync(existing => existing.Id == jobId, cancellationToken)
                ?? throw new InvalidOperationException("Job row is gone (deleted while queued?).");

            if (job.Status != ExportJobStatus.Queued && job.Status != ExportJobStatus.Processing)
            {
                return;
            }

            job.MarkProcessing(DateTimeOffset.UtcNow);
            await dbContext.SaveChangesAsync(cancellationToken);

            var (title, sheets, rowsCount) = await dataBuilder.BuildAsync(
                job.Kind, job.FromDate, job.ToDate, cancellationToken);

            var workbook = excelExporter.BuildWorkbook(title, sheets);
            var periodTag = job.FromDate.HasValue || job.ToDate.HasValue
                ? $"-{job.FromDate:yyyyMMdd}-{job.ToDate:yyyyMMdd}"
                : string.Empty;
            var fileName = $"sugarista-{job.Kind}{periodTag}-{job.CreatedAt:yyyyMMdd-HHmm}.xlsx";
            var storagePath = await fileStore.SaveAsync(
                fileName, workbook.Content, cancellationToken);

            job.MarkCompleted(
                fileName,
                storagePath,
                rowsCount,
                workbook.Content.Length,
                DateTimeOffset.UtcNow);
            await dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Export job {JobId} ({Kind}) completed: {Rows} rows, {Size} bytes.",
                jobId,
                job.Kind,
                rowsCount,
                workbook.Content.Length);

            await PruneOldJobsAsync(dbContext, fileStore);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Export job {JobId} failed.", jobId);

            var jobEntity = await dbContext.ExportJobs
                .FirstOrDefaultAsync(existing => existing.Id == jobId, CancellationToken.None);
            if (jobEntity != null
                && jobEntity.Status != ExportJobStatus.Completed)
            {
                jobEntity.MarkFailed(
                    $"ساخت خروجی ناموفق بود: {exception.Message}",
                    DateTimeOffset.UtcNow);
                await dbContext.SaveChangesAsync(CancellationToken.None);
            }
        }
    }

    private async Task RecoverPersistedJobsAsync(CancellationToken cancellationToken)
    {
        var recovered = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

                var pendingJobIds = await dbContext.ExportJobs
                    .Where(job => job.Status == ExportJobStatus.Queued
                        || job.Status == ExportJobStatus.Processing)
                    .OrderBy(job => job.CreatedAt)
                    .Select(job => job.Id)
                    .ToListAsync(cancellationToken);

                foreach (var jobId in pendingJobIds)
                {
                    await _queue.EnqueueAsync(jobId, cancellationToken);
                }

                recovered = pendingJobIds.Count;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "Could not recover export jobs yet (database not ready?). Retrying in 10s.");

                await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
                continue;
            }

            break;
        }

        if (recovered > 0)
        {
            _logger.LogInformation(
                "Recovered {Count} pending export job(s) after host restart.",
                recovered);
        }
    }

    private async Task MarkJobFailedAsync(Guid jobId, string message)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

            var job = await dbContext.ExportJobs
                .FirstOrDefaultAsync(existing => existing.Id == jobId, CancellationToken.None);
            if (job != null && job.Status != ExportJobStatus.Completed)
            {
                job.MarkFailed($"ساخت خروجی ناموفق بود: {message}", DateTimeOffset.UtcNow);
                await dbContext.SaveChangesAsync(CancellationToken.None);
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Could not record failure for export job {JobId}.", jobId);
        }
    }

    private async Task PruneOldJobsAsync(IApplicationDbContext dbContext, IExportFileStore fileStore)
    {
        try
        {
            var jobs = await dbContext.ExportJobs
                .OrderByDescending(job => job.CreatedAt)
                .ToListAsync(CancellationToken.None);

            var stale = jobs.Count > MaxRetainedJobs
                ? jobs.Skip(MaxRetainedJobs).Where(job => job.Status is ExportJobStatus.Completed or ExportJobStatus.Failed).ToList()
                : [];

            foreach (var job in stale)
            {
                if (!string.IsNullOrEmpty(job.StoragePath))
                {
                    fileStore.Delete(job.StoragePath);
                }

                dbContext.ExportJobs.Remove(job);
            }

            if (stale.Count > 0)
            {
                await dbContext.SaveChangesAsync(CancellationToken.None);
                _logger.LogInformation("Pruned {Count} old export job(s).", stale.Count);
            }
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Could not prune old export jobs.");
        }
    }
}
