using Sugarista.Domain.Entities;

namespace Sugarista.Application.Exports;

public record ExportJobDto(
    Guid Id,
    string Kind,
    DateOnly? FromDate,
    DateOnly? ToDate,
    string Status,
    string FileName,
    string? ErrorMessage,
    long RowsCount,
    long FileSizeBytes,
    string RequestedBy,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset CreatedAt);

public static class ExportJobMapper
{
    public static ExportJobDto ToDto(ExportJob job) => new(
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
        job.CreatedAt);
}
