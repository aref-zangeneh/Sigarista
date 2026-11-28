using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sugarista.Api.Authorization;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Domain.Constants;
using Sugarista.Domain.Entities;
using Sugarista.Domain.Exceptions;
using Sugarista.Infrastructure.Backups;

namespace Sugarista.Api.Controllers;

/// <summary>
/// Enterprise Backup Management, Scheduling, Multi-Cloud Upload (S3/Arvan/Liara/R2/MinIO)
/// and Backup Health Monitoring controller.
/// </summary>
[Authorize]
[Route("api/admin/backups")]
public class AdminBackupsController : ApiControllerBase
{
    private readonly IApplicationDbContext _dbContext;
    private readonly BackupExecutionService _backupService;
    private readonly S3V4CloudUploader _cloudUploader;

    public AdminBackupsController(
        IApplicationDbContext dbContext,
        BackupExecutionService backupService,
        S3V4CloudUploader cloudUploader)
    {
        _dbContext = dbContext;
        _backupService = backupService;
        _cloudUploader = cloudUploader;
    }

    [HttpGet("overview")]
    [RequirePermission(AdminPermissions.SiteSettings.View)]
    public async Task<ActionResult<BackupOverviewDto>> GetOverview(CancellationToken cancellationToken)
    {
        var records = await _dbContext.BackupRecords
            .AsNoTracking()
            .OrderByDescending(r => r.StartedAt)
            .Take(80)
            .ToListAsync(cancellationToken);

        var jobs = await _dbContext.BackupJobs
            .AsNoTracking()
            .Include(j => j.CloudTarget)
            .OrderByDescending(j => j.IsEnabled)
            .ThenBy(j => j.Name)
            .ToListAsync(cancellationToken);

        var targets = await _dbContext.BackupCloudTargets
            .AsNoTracking()
            .OrderByDescending(t => t.IsDefault)
            .ThenBy(t => t.Name)
            .ToListAsync(cancellationToken);

        var completedCount = records.Count(r => r.Status == BackupStatus.Completed);
        var failedCount = records.Count(r => r.Status == BackupStatus.Failed);
        var totalAttempts = completedCount + failedCount;
        var successRate = totalAttempts > 0 ? Math.Round((double)completedCount / totalAttempts * 100.0, 1) : 100.0;
        var totalStorageBytes = records
            .Where(r => r.Status == BackupStatus.Completed)
            .Sum(r => r.FileSizeBytes);

        var lastSuccess = records.FirstOrDefault(r => r.Status == BackupStatus.Completed);
        var nextJobRun = jobs
            .Where(j => j.IsEnabled && j.NextRunAt.HasValue)
            .OrderBy(j => j.NextRunAt)
            .Select(j => j.NextRunAt)
            .FirstOrDefault();

        var freeDiskGb = GetBackupDriveFreeGb();

        var health = failedCount > 0 && records.FirstOrDefault()?.Status == BackupStatus.Failed
            ? "Warning"
            : lastSuccess is null
                ? "NeedsSetup"
                : "Healthy";

        return Ok(new BackupOverviewDto(
            Health: health,
            SuccessRatePercent: successRate,
            TotalBackupsCount: records.Count,
            CompletedCount: completedCount,
            FailedCount: failedCount,
            TotalBackupStorageBytes: totalStorageBytes,
            FreeDiskGb: freeDiskGb,
            LastSuccessfulBackupAt: lastSuccess?.CompletedAt,
            LastSuccessfulBackupSizeBytes: lastSuccess?.FileSizeBytes ?? 0,
            NextScheduledRunAt: nextJobRun,
            ActiveJobsCount: jobs.Count(j => j.IsEnabled),
            CloudTargetsCount: targets.Count(t => t.IsEnabled),
            CloudUploadedCount: records.Count(r => r.CloudStatus == BackupCloudUploadStatus.Uploaded),
            Records: records.Select(r => MapRecord(r, _backupService.ResolveSafeFilePath(r.FileName))).ToList(),
            Jobs: jobs.Select(MapJob).ToList(),
            CloudTargets: targets.Select(MapTarget).ToList()));
    }

    [HttpPost("run")]
    [RequirePermission(AdminPermissions.SiteSettings.Update)]
    public async Task<ActionResult<BackupRecordDto>> RunManualBackup(
        [FromBody] RunManualBackupRequest request,
        CancellationToken cancellationToken)
    {
        var adminName = ResolveAdminName();
        var record = await _backupService.RunBackupAsync(
            scope: request.Scope,
            triggerType: "Manual",
            jobId: null,
            jobName: "بکاپ دستی",
            cloudTargetId: request.CloudTargetId,
            retentionCount: null,
            createdByName: adminName,
            cancellationToken: cancellationToken);

        return Ok(MapRecord(record, _backupService.ResolveSafeFilePath(record.FileName)));
    }

    [HttpGet("{id:guid}/download")]
    [RequirePermission(AdminPermissions.SiteSettings.View)]
    public async Task<IActionResult> Download(Guid id, CancellationToken cancellationToken)
    {
        var record = await _dbContext.BackupRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (record is null || record.Status != BackupStatus.Completed)
            return NotFound(new { message = "فایل بکاپ موردنظر پیدا نشد." });

        var safePath = _backupService.ResolveSafeFilePath(record.FileName);
        if (safePath is null || !System.IO.File.Exists(safePath))
            return NotFound(new { message = "فایل فیزیکی بکاپ روی دیسک موجود نیست." });

        return PhysicalFile(safePath, "application/zip", record.FileName, enableRangeProcessing: true);
    }

    [HttpPost("{id:guid}/verify")]
    [RequirePermission(AdminPermissions.SiteSettings.View)]
    public async Task<ActionResult<object>> Verify(Guid id, CancellationToken cancellationToken)
    {
        var (valid, message) = await _backupService.VerifyBackupIntegrityAsync(id, cancellationToken);
        return Ok(new { valid, message });
    }

    [HttpPost("{id:guid}/upload-cloud")]
    [RequirePermission(AdminPermissions.SiteSettings.Update)]
    public async Task<ActionResult<object>> UploadToCloud(
        Guid id,
        [FromBody] UploadBackupToCloudRequest request,
        CancellationToken cancellationToken)
    {
        var (success, message) = await _backupService.UploadExistingBackupToCloudAsync(
            id, request.CloudTargetId, cancellationToken);
        if (!success) return BadRequest(new { message });
        return Ok(new { success, message });
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(AdminPermissions.SiteSettings.Update)]
    public async Task<IActionResult> DeleteBackup(Guid id, CancellationToken cancellationToken)
    {
        var record = await _dbContext.BackupRecords
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (record is null) return NotFound();

        var safePath = _backupService.ResolveSafeFilePath(record.FileName);
        if (safePath is not null && System.IO.File.Exists(safePath))
        {
            try { System.IO.File.Delete(safePath); } catch { }
        }

        _dbContext.BackupRecords.Remove(record);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    // ── Scheduled Backup Jobs ───────────────────────────────────────────────

    [HttpPost("jobs")]
    [RequirePermission(AdminPermissions.SiteSettings.Update)]
    public async Task<ActionResult<BackupJobDto>> CreateJob(
        [FromBody] UpsertBackupJobRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var job = new BackupJob(
                request.Name ?? string.Empty,
                request.Scope,
                request.ScheduleType,
                request.IntervalHours,
                request.RunHourTehran,
                request.RunMinuteTehran,
                request.DayOfWeek,
                request.RetentionCount,
                request.CloudTargetId,
                request.IsEnabled);

            _dbContext.BackupJobs.Add(job);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return Ok(MapJob(job));
        }
        catch (DomainException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("jobs/{id:guid}")]
    [RequirePermission(AdminPermissions.SiteSettings.Update)]
    public async Task<ActionResult<BackupJobDto>> UpdateJob(
        Guid id,
        [FromBody] UpsertBackupJobRequest request,
        CancellationToken cancellationToken)
    {
        var job = await _dbContext.BackupJobs.FirstOrDefaultAsync(j => j.Id == id, cancellationToken);
        if (job is null) return NotFound();

        try
        {
            job.Update(
                request.Name ?? job.Name,
                request.Scope,
                request.ScheduleType,
                request.IntervalHours,
                request.RunHourTehran,
                request.RunMinuteTehran,
                request.DayOfWeek,
                request.RetentionCount,
                request.CloudTargetId,
                request.IsEnabled);

            await _dbContext.SaveChangesAsync(cancellationToken);
            return Ok(MapJob(job));
        }
        catch (DomainException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("jobs/{id:guid}/run-now")]
    [RequirePermission(AdminPermissions.SiteSettings.Update)]
    public async Task<ActionResult<BackupRecordDto>> RunJobNow(Guid id, CancellationToken cancellationToken)
    {
        var job = await _dbContext.BackupJobs.FirstOrDefaultAsync(j => j.Id == id, cancellationToken);
        if (job is null) return NotFound();

        var record = await _backupService.RunBackupAsync(
            scope: job.Scope,
            triggerType: "ManualJob",
            jobId: job.Id,
            jobName: job.Name,
            cloudTargetId: job.CloudTargetId,
            retentionCount: job.RetentionCount,
            createdByName: ResolveAdminName(),
            cancellationToken: cancellationToken);

        job.MarkExecuted(DateTimeOffset.UtcNow, record.Status == BackupStatus.Completed);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(MapRecord(record, _backupService.ResolveSafeFilePath(record.FileName)));
    }

    [HttpDelete("jobs/{id:guid}")]
    [RequirePermission(AdminPermissions.SiteSettings.Update)]
    public async Task<IActionResult> DeleteJob(Guid id, CancellationToken cancellationToken)
    {
        var job = await _dbContext.BackupJobs.FirstOrDefaultAsync(j => j.Id == id, cancellationToken);
        if (job is null) return NotFound();

        _dbContext.BackupJobs.Remove(job);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    // ── Cloud Storage Targets (Multi-Cloud) ─────────────────────────────────

    [HttpPost("cloud-targets")]
    [RequirePermission(AdminPermissions.SiteSettings.Update)]
    public async Task<ActionResult<BackupCloudTargetDto>> CreateCloudTarget(
        [FromBody] UpsertBackupCloudTargetRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (request.IsDefault)
            {
                await ClearDefaultTargetsAsync(null, cancellationToken);
            }

            var target = new BackupCloudTarget(
                request.Name ?? string.Empty,
                request.Provider,
                request.EndpointUrl ?? string.Empty,
                request.BucketName ?? string.Empty,
                request.Region ?? "default",
                request.AccessKey ?? string.Empty,
                request.SecretKey ?? string.Empty,
                request.KeyPrefix,
                request.IsDefault,
                request.IsEnabled);

            _dbContext.BackupCloudTargets.Add(target);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return Ok(MapTarget(target));
        }
        catch (DomainException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("cloud-targets/{id:guid}")]
    [RequirePermission(AdminPermissions.SiteSettings.Update)]
    public async Task<ActionResult<BackupCloudTargetDto>> UpdateCloudTarget(
        Guid id,
        [FromBody] UpsertBackupCloudTargetRequest request,
        CancellationToken cancellationToken)
    {
        var target = await _dbContext.BackupCloudTargets.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (target is null) return NotFound();

        try
        {
            if (request.IsDefault)
            {
                await ClearDefaultTargetsAsync(id, cancellationToken);
            }

            target.Update(
                request.Name ?? target.Name,
                request.Provider,
                request.EndpointUrl ?? target.EndpointUrl,
                request.BucketName ?? target.BucketName,
                request.Region ?? target.Region,
                request.AccessKey,
                request.SecretKey,
                request.KeyPrefix,
                request.IsDefault,
                request.IsEnabled);

            await _dbContext.SaveChangesAsync(cancellationToken);
            return Ok(MapTarget(target));
        }
        catch (DomainException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("cloud-targets/{id:guid}/test")]
    [RequirePermission(AdminPermissions.SiteSettings.Update)]
    public async Task<ActionResult<object>> TestCloudTarget(Guid id, CancellationToken cancellationToken)
    {
        var target = await _dbContext.BackupCloudTargets.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (target is null) return NotFound();

        var (success, message) = await _cloudUploader.TestConnectionAsync(target, cancellationToken);
        target.RecordTestResult(success, message, DateTimeOffset.UtcNow);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new { success, message });
    }

    [HttpDelete("cloud-targets/{id:guid}")]
    [RequirePermission(AdminPermissions.SiteSettings.Update)]
    public async Task<IActionResult> DeleteCloudTarget(Guid id, CancellationToken cancellationToken)
    {
        var target = await _dbContext.BackupCloudTargets.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (target is null) return NotFound();

        _dbContext.BackupCloudTargets.Remove(target);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private async Task ClearDefaultTargetsAsync(Guid? excludeId, CancellationToken cancellationToken)
    {
        var existing = await _dbContext.BackupCloudTargets
            .Where(t => t.IsDefault && (!excludeId.HasValue || t.Id != excludeId.Value))
            .ToListAsync(cancellationToken);
        foreach (var t in existing) t.SetDefault(false);
    }

    private double GetBackupDriveFreeGb()
    {
        try
        {
            var root = Path.GetPathRoot(_backupService.BackupRootDirectory);
            if (string.IsNullOrWhiteSpace(root)) return 0;
            var di = new DriveInfo(root);
            return Math.Round(di.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0), 1);
        }
        catch
        {
            return 0;
        }
    }

    private string? ResolveAdminName()
        => User.FindFirstValue("display_name") ?? User.FindFirstValue(ClaimTypes.Name) ?? User.Identity?.Name;

    private static string MaskSecret(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        return value.Length <= 8 ? "••••••••" : $"{value[..4]}••••{value[^4..]}";
    }

    private static BackupRecordDto MapRecord(BackupRecord r, string? safePath) => new(
        r.Id,
        r.BackupCode,
        r.JobId,
        r.JobName ?? string.Empty,
        r.TriggerType,
        r.Scope.ToString(),
        r.Status.ToString(),
        r.FileName,
        r.FileSizeBytes,
        r.Sha256Checksum ?? string.Empty,
        r.DatabaseEngine ?? string.Empty,
        r.DurationMs,
        r.StartedAt,
        r.CompletedAt,
        r.ErrorMessage ?? string.Empty,
        r.CreatedByName ?? string.Empty,
        r.CloudTargetId,
        r.CloudTargetName ?? string.Empty,
        r.CloudStatus.ToString(),
        r.CloudObjectKey ?? string.Empty,
        r.CloudUploadedAt,
        r.CloudErrorMessage ?? string.Empty,
        FileExistsOnDisk: safePath is not null && System.IO.File.Exists(safePath));

    private static BackupJobDto MapJob(BackupJob j) => new(
        j.Id,
        j.Name,
        j.Scope.ToString(),
        j.ScheduleType.ToString(),
        j.IntervalHours,
        j.RunHourTehran,
        j.RunMinuteTehran,
        j.DayOfWeek,
        j.RetentionCount,
        j.CloudTargetId,
        j.CloudTarget?.Name ?? string.Empty,
        j.IsEnabled,
        j.LastRunAt,
        j.NextRunAt,
        j.LastRunStatus ?? string.Empty);

    private static BackupCloudTargetDto MapTarget(BackupCloudTarget t) => new(
        t.Id,
        t.Name,
        t.Provider.ToString(),
        t.EndpointUrl,
        t.BucketName,
        t.Region,
        MaskSecret(t.AccessKey),
        !string.IsNullOrWhiteSpace(t.SecretKey),
        t.KeyPrefix,
        t.IsDefault,
        t.IsEnabled,
        t.LastTestedAt,
        t.LastTestSuccess,
        t.LastTestMessage ?? string.Empty);
}

public record BackupOverviewDto(
    string Health,
    double SuccessRatePercent,
    int TotalBackupsCount,
    int CompletedCount,
    int FailedCount,
    long TotalBackupStorageBytes,
    double FreeDiskGb,
    DateTimeOffset? LastSuccessfulBackupAt,
    long LastSuccessfulBackupSizeBytes,
    DateTimeOffset? NextScheduledRunAt,
    int ActiveJobsCount,
    int CloudTargetsCount,
    int CloudUploadedCount,
    IReadOnlyList<BackupRecordDto> Records,
    IReadOnlyList<BackupJobDto> Jobs,
    IReadOnlyList<BackupCloudTargetDto> CloudTargets);

public record BackupRecordDto(
    Guid Id,
    string BackupCode,
    Guid? JobId,
    string JobName,
    string TriggerType,
    string Scope,
    string Status,
    string FileName,
    long FileSizeBytes,
    string Sha256Checksum,
    string DatabaseEngine,
    long DurationMs,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    string ErrorMessage,
    string CreatedByName,
    Guid? CloudTargetId,
    string CloudTargetName,
    string CloudStatus,
    string CloudObjectKey,
    DateTimeOffset? CloudUploadedAt,
    string CloudErrorMessage,
    bool FileExistsOnDisk);

public record BackupJobDto(
    Guid Id,
    string Name,
    string Scope,
    string ScheduleType,
    int IntervalHours,
    int RunHourTehran,
    int RunMinuteTehran,
    int? DayOfWeek,
    int RetentionCount,
    Guid? CloudTargetId,
    string CloudTargetName,
    bool IsEnabled,
    DateTimeOffset? LastRunAt,
    DateTimeOffset? NextRunAt,
    string LastRunStatus);

public record BackupCloudTargetDto(
    Guid Id,
    string Name,
    string Provider,
    string EndpointUrl,
    string BucketName,
    string Region,
    string MaskedAccessKey,
    bool HasSecretKey,
    string KeyPrefix,
    bool IsDefault,
    bool IsEnabled,
    DateTimeOffset? LastTestedAt,
    bool? LastTestSuccess,
    string LastTestMessage);

public record RunManualBackupRequest(
    BackupScope Scope = BackupScope.FullSystem,
    Guid? CloudTargetId = null);

public record UploadBackupToCloudRequest(Guid CloudTargetId);

public record UpsertBackupJobRequest(
    string? Name,
    BackupScope Scope = BackupScope.FullSystem,
    BackupScheduleType ScheduleType = BackupScheduleType.Daily,
    int IntervalHours = 6,
    int RunHourTehran = 3,
    int RunMinuteTehran = 0,
    int? DayOfWeek = 5,
    int RetentionCount = 10,
    Guid? CloudTargetId = null,
    bool IsEnabled = true);

public record UpsertBackupCloudTargetRequest(
    string? Name,
    BackupCloudProvider Provider = BackupCloudProvider.ArvanCloud,
    string? EndpointUrl = null,
    string? BucketName = null,
    string? Region = "default",
    string? AccessKey = null,
    string? SecretKey = null,
    string? KeyPrefix = "sugarista-backups/",
    bool IsDefault = false,
    bool IsEnabled = true);
