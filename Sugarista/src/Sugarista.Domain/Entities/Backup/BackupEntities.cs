using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

public enum BackupScope
{
    DatabaseOnly = 0,
    UploadsOnly = 1,
    FullSystem = 2,
}

public enum BackupScheduleType
{
    Manual = 0,
    Hourly = 1,
    Daily = 2,
    Weekly = 3,
}

public enum BackupStatus
{
    Running = 0,
    Completed = 1,
    Failed = 2,
}

public enum BackupCloudProvider
{
    ArvanCloud = 0,
    Liara = 1,
    ParsPack = 2,
    AwsS3 = 3,
    CloudflareR2 = 4,
    MinIO = 5,
    CustomWebhook = 6,
}

public enum BackupCloudUploadStatus
{
    None = 0,
    Uploading = 1,
    Uploaded = 2,
    Failed = 3,
}

public class BackupCloudTarget : BaseAuditableEntity
{
    private BackupCloudTarget()
    {
    }

    public BackupCloudTarget(
        string name,
        BackupCloudProvider provider,
        string endpointUrl,
        string bucketName,
        string region,
        string accessKey,
        string secretKey,
        string? keyPrefix = "sugarista-backups/",
        bool isDefault = false,
        bool isEnabled = true)
    {
        Update(name, provider, endpointUrl, bucketName, region, accessKey, secretKey, keyPrefix, isDefault, isEnabled);
    }

    public string Name { get; private set; } = string.Empty;

    public BackupCloudProvider Provider { get; private set; } = BackupCloudProvider.ArvanCloud;

    public string EndpointUrl { get; private set; } = string.Empty;

    public string BucketName { get; private set; } = string.Empty;

    public string Region { get; private set; } = "default";

    public string AccessKey { get; private set; } = string.Empty;

    public string SecretKey { get; private set; } = string.Empty;

    public string KeyPrefix { get; private set; } = "sugarista-backups/";

    public bool IsDefault { get; private set; }

    public bool IsEnabled { get; private set; } = true;

    public DateTimeOffset? LastTestedAt { get; private set; }

    public bool? LastTestSuccess { get; private set; }

    public string? LastTestMessage { get; private set; }

    public void Update(
        string name,
        BackupCloudProvider provider,
        string endpointUrl,
        string bucketName,
        string region,
        string? accessKey,
        string? secretKey,
        string? keyPrefix,
        bool isDefault,
        bool isEnabled)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("نام مقصد ابری الزامی است.");
        if (string.IsNullOrWhiteSpace(endpointUrl))
            throw new DomainException("آدرس Endpoint مقصد ابری الزامی است.");
        if (provider != BackupCloudProvider.CustomWebhook && string.IsNullOrWhiteSpace(bucketName))
            throw new DomainException("نام Bucket الزامی است.");

        Name = Truncate(name.Trim(), 120);
        Provider = provider;
        EndpointUrl = Truncate(endpointUrl.Trim().TrimEnd('/'), 300);
        BucketName = Truncate((bucketName ?? string.Empty).Trim(), 120);
        Region = string.IsNullOrWhiteSpace(region) ? "default" : Truncate(region.Trim(), 60);
        if (!string.IsNullOrWhiteSpace(accessKey))
            AccessKey = Truncate(accessKey.Trim(), 200);
        if (!string.IsNullOrWhiteSpace(secretKey))
            SecretKey = Truncate(secretKey.Trim(), 300);

        var cleanedPrefix = (keyPrefix ?? "sugarista-backups/").Trim().TrimStart('/');
        if (cleanedPrefix.Length > 0 && !cleanedPrefix.EndsWith('/'))
            cleanedPrefix += "/";
        KeyPrefix = Truncate(cleanedPrefix, 120);

        IsDefault = isDefault;
        IsEnabled = isEnabled;
    }

    public void SetDefault(bool isDefault) => IsDefault = isDefault;

    public void RecordTestResult(bool success, string message, DateTimeOffset testedAt)
    {
        LastTestedAt = testedAt;
        LastTestSuccess = success;
        LastTestMessage = Truncate((message ?? string.Empty).Trim(), 400);
    }

    private static string Truncate(string value, int max) => value.Length > max ? value[..max] : value;
}

public class BackupJob : BaseAuditableEntity
{
    private BackupJob()
    {
    }

    public BackupJob(
        string name,
        BackupScope scope,
        BackupScheduleType scheduleType,
        int intervalHours,
        int runHourTehran,
        int runMinuteTehran,
        int? dayOfWeek,
        int retentionCount,
        Guid? cloudTargetId,
        bool isEnabled)
    {
        Update(name, scope, scheduleType, intervalHours, runHourTehran, runMinuteTehran, dayOfWeek, retentionCount, cloudTargetId, isEnabled);
    }

    public string Name { get; private set; } = string.Empty;

    public BackupScope Scope { get; private set; } = BackupScope.FullSystem;

    public BackupScheduleType ScheduleType { get; private set; } = BackupScheduleType.Daily;

    public int IntervalHours { get; private set; } = 6;

    /// <summary>Execution hour in Asia/Tehran local time (0-23).</summary>
    public int RunHourTehran { get; private set; } = 3;

    /// <summary>Execution minute in Asia/Tehran local time (0-59).</summary>
    public int RunMinuteTehran { get; private set; }

    /// <summary>0=Sunday .. 6=Saturday (used for Weekly schedule).</summary>
    public int? DayOfWeek { get; private set; }

    /// <summary>Maximum backup snapshots to keep locally for this job (older ones auto-pruned).</summary>
    public int RetentionCount { get; private set; } = 10;

    public Guid? CloudTargetId { get; private set; }

    public BackupCloudTarget? CloudTarget { get; private set; }

    public bool IsEnabled { get; private set; } = true;

    public DateTimeOffset? LastRunAt { get; private set; }

    public DateTimeOffset? NextRunAt { get; private set; }

    public string? LastRunStatus { get; private set; }

    public void Update(
        string name,
        BackupScope scope,
        BackupScheduleType scheduleType,
        int intervalHours,
        int runHourTehran,
        int runMinuteTehran,
        int? dayOfWeek,
        int retentionCount,
        Guid? cloudTargetId,
        bool isEnabled)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("نام جاب بکاپ الزامی است.");

        Name = name.Trim().Length > 140 ? name.Trim()[..140] : name.Trim();
        Scope = scope;
        ScheduleType = scheduleType;
        IntervalHours = Math.Clamp(intervalHours, 1, 72);
        RunHourTehran = Math.Clamp(runHourTehran, 0, 23);
        RunMinuteTehran = Math.Clamp(runMinuteTehran, 0, 59);
        DayOfWeek = dayOfWeek.HasValue ? Math.Clamp(dayOfWeek.Value, 0, 6) : null;
        RetentionCount = Math.Clamp(retentionCount, 1, 100);
        CloudTargetId = cloudTargetId;
        IsEnabled = isEnabled;
        NextRunAt = ComputeNextRunUtc(DateTimeOffset.UtcNow);
    }

    public void MarkExecuted(DateTimeOffset executedAtUtc, bool success)
    {
        LastRunAt = executedAtUtc;
        LastRunStatus = success ? "Completed" : "Failed";
        NextRunAt = ComputeNextRunUtc(executedAtUtc.AddMinutes(1));
    }

    public DateTimeOffset? ComputeNextRunUtc(DateTimeOffset fromUtc)
    {
        if (!IsEnabled || ScheduleType == BackupScheduleType.Manual)
            return null;

        if (ScheduleType == BackupScheduleType.Hourly)
        {
            return fromUtc.AddHours(IntervalHours);
        }

        var tehranOffset = TimeSpan.FromHours(3.5);
        var tehranNow = fromUtc.ToOffset(tehranOffset);

        if (ScheduleType == BackupScheduleType.Daily)
        {
            var candidate = new DateTimeOffset(
                tehranNow.Year, tehranNow.Month, tehranNow.Day,
                RunHourTehran, RunMinuteTehran, 0, tehranOffset);
            if (candidate <= tehranNow)
                candidate = candidate.AddDays(1);
            return candidate.ToUniversalTime();
        }

        // Weekly
        var targetDow = (System.DayOfWeek)(DayOfWeek ?? 5); // Default Friday
        var daysUntil = ((int)targetDow - (int)tehranNow.DayOfWeek + 7) % 7;
        var weeklyCandidate = new DateTimeOffset(
            tehranNow.Year, tehranNow.Month, tehranNow.Day,
            RunHourTehran, RunMinuteTehran, 0, tehranOffset).AddDays(daysUntil);
        if (weeklyCandidate <= tehranNow)
            weeklyCandidate = weeklyCandidate.AddDays(7);
        return weeklyCandidate.ToUniversalTime();
    }
}

public class BackupRecord : BaseAuditableEntity
{
    private BackupRecord()
    {
    }

    public BackupRecord(
        string backupCode,
        BackupScope scope,
        string triggerType,
        Guid? jobId = null,
        string? jobName = null,
        Guid? cloudTargetId = null,
        string? cloudTargetName = null,
        string? createdByName = null)
    {
        BackupCode = backupCode.Trim();
        Scope = scope;
        TriggerType = string.IsNullOrWhiteSpace(triggerType) ? "Manual" : triggerType.Trim();
        JobId = jobId;
        JobName = jobName;
        CloudTargetId = cloudTargetId;
        CloudTargetName = cloudTargetName;
        CreatedByName = createdByName;
        Status = BackupStatus.Running;
        CloudStatus = BackupCloudUploadStatus.None;
        StartedAt = DateTimeOffset.UtcNow;
    }

    public string BackupCode { get; private set; } = string.Empty;

    public Guid? JobId { get; private set; }

    public string? JobName { get; private set; }

    public string TriggerType { get; private set; } = "Manual";

    public BackupScope Scope { get; private set; } = BackupScope.FullSystem;

    public BackupStatus Status { get; private set; } = BackupStatus.Running;

    public string FileName { get; private set; } = string.Empty;

    public long FileSizeBytes { get; private set; }

    public string? Sha256Checksum { get; private set; }

    public string? DatabaseEngine { get; private set; }

    public long DurationMs { get; private set; }

    public DateTimeOffset StartedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public string? ErrorMessage { get; private set; }

    public string? CreatedByName { get; private set; }

    public Guid? CloudTargetId { get; private set; }

    public string? CloudTargetName { get; private set; }

    public BackupCloudUploadStatus CloudStatus { get; private set; } = BackupCloudUploadStatus.None;

    public string? CloudObjectKey { get; private set; }

    public DateTimeOffset? CloudUploadedAt { get; private set; }

    public string? CloudErrorMessage { get; private set; }

    public void MarkCompleted(string fileName, long fileSizeBytes, string sha256, string databaseEngine, long durationMs)
    {
        Status = BackupStatus.Completed;
        FileName = fileName;
        FileSizeBytes = fileSizeBytes;
        Sha256Checksum = sha256;
        DatabaseEngine = databaseEngine;
        DurationMs = durationMs;
        CompletedAt = DateTimeOffset.UtcNow;
        ErrorMessage = null;
    }

    public void MarkFailed(string error, long durationMs)
    {
        Status = BackupStatus.Failed;
        ErrorMessage = error.Length > 1000 ? error[..1000] : error;
        DurationMs = durationMs;
        CompletedAt = DateTimeOffset.UtcNow;
    }

    public void MarkCloudUploading(Guid targetId, string targetName)
    {
        CloudTargetId = targetId;
        CloudTargetName = targetName;
        CloudStatus = BackupCloudUploadStatus.Uploading;
        CloudErrorMessage = null;
    }

    public void MarkCloudUploaded(string objectKey)
    {
        CloudStatus = BackupCloudUploadStatus.Uploaded;
        CloudObjectKey = objectKey;
        CloudUploadedAt = DateTimeOffset.UtcNow;
        CloudErrorMessage = null;
    }

    public void MarkCloudFailed(string error)
    {
        CloudStatus = BackupCloudUploadStatus.Failed;
        CloudErrorMessage = error.Length > 1000 ? error[..1000] : error;
    }
}
