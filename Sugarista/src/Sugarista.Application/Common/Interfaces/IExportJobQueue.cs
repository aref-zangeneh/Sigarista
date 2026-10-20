using System.Threading.Channels;

namespace Sugarista.Application.Common.Interfaces;

/// <summary>Payload handed to the background export runner.</summary>
public record ExportJobPayload(Guid JobId);

/// <summary>In-process queue between the API request pipeline and the background export runner.</summary>
public interface IExportJobQueue
{
    /// <summary>Queues a job without ever blocking the request.</summary>
    ValueTask EnqueueAsync(Guid jobId, CancellationToken cancellationToken = default);

    /// <summary>Reads the next queued job id (one supervisor loop).</summary>
    ValueTask<ExportJobPayload?> DequeueAsync(CancellationToken cancellationToken = default);

    /// <summary>Re-enqueues a skipped item (used on startup sweep).</summary>
    void Return(ExportJobPayload payload);
}
