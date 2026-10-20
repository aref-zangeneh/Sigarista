using Sugarista.Domain.Common;

namespace Sugarista.Domain.Entities;

public enum ExportJobStatus
{
    Queued,
    Processing,
    Completed,
    Failed,
}

/// <summary>
/// Async Excel export request: queued, processed by the background runner and
/// downloaded as a file afterwards — never blocking the HTTP request.
/// </summary>
public class ExportJob : BaseAuditableEntity
{
    public ExportJob(
        string kind,
        DateOnly? fromDate,
        DateOnly? toDate,
        string requestedBy)
    {
        Kind = kind.Trim().ToLowerInvariant();
        FromDate = fromDate;
        ToDate = toDate;
        RequestedBy = requestedBy.Trim();
        Status = ExportJobStatus.Queued;
        RowsCount = 0;
        FileSizeBytes = 0;
    }

    public string Kind { get; private set; } = string.Empty;

    public DateOnly? FromDate { get; private set; }

    public DateOnly? ToDate { get; private set; }

    public string RequestedBy { get; private set; } = string.Empty;

    public ExportJobStatus Status { get; private set; }

    public string? FileName { get; private set; }

    /// <summary>Local storage path of the finished file (internal).</summary>
    public string? StoragePath { get; private set; }

    public string? ErrorMessage { get; private set; }

    public long RowsCount { get; private set; }

    public long FileSizeBytes { get; private set; }

    public DateTimeOffset? StartedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public void MarkProcessing(DateTimeOffset now)
    {
        Status = ExportJobStatus.Processing;
        StartedAt = now;
    }

    public void MarkCompleted(string fileName, string storagePath, long rowsCount, long fileSizeBytes, DateTimeOffset now)
    {
        Status = ExportJobStatus.Completed;
        FileName = fileName;
        StoragePath = storagePath;
        RowsCount = rowsCount;
        FileSizeBytes = fileSizeBytes;
        CompletedAt = now;
    }

    public void MarkFailed(string errorMessage, DateTimeOffset now)
    {
        Status = ExportJobStatus.Failed;
        ErrorMessage = errorMessage.Length > 1000 ? errorMessage[..1000] : errorMessage;
        CompletedAt = now;
    }
}
