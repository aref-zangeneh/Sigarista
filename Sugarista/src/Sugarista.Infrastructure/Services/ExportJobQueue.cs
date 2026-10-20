using System.Threading.Channels;

namespace Sugarista.Infrastructure.Services;

/// <summary>
/// Channel-based in-memory queue for export jobs. Fast to enqueue (non-blocking);
/// the background runner processes items sequentially so heavy exports never
/// run parallel against the database.
/// </summary>
public class ExportJobQueue : IExportJobQueue
{
    private readonly Channel<ExportJobPayload> _channel = Channel.CreateBounded<ExportJobPayload>(
        new BoundedChannelOptions(200)
        {
            SingleReader = true,
            FullMode = BoundedChannelFullMode.Wait,
        });

    public async ValueTask EnqueueAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        while (await _channel.Writer.WaitToWriteAsync(cancellationToken))
        {
            if (_channel.Writer.TryWrite(new ExportJobPayload(jobId)))
            {
                return;
            }
        }

        throw new InvalidOperationException("Export job queue is not accepting items.");
    }

    public async ValueTask<ExportJobPayload?> DequeueAsync(CancellationToken cancellationToken = default)
    {
        await _channel.Reader.WaitToReadAsync(cancellationToken);
        return _channel.Reader.TryRead(out var payload) ? payload : null;
    }

    public void Return(ExportJobPayload payload)
    {
        _channel.Writer.TryWrite(payload);
    }
}
