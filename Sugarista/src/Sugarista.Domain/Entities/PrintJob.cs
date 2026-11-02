using Sugarista.Domain.Common;

namespace Sugarista.Domain.Entities;

/// <summary>Which copy of the receipt this job prints.</summary>
public enum PrintJobCopyType
{
    /// <summary>Full customer receipt: items, totals and payment details.</summary>
    Customer,

    /// <summary>Kitchen/barista ticket: items, quantities and notes only — no money.</summary>
    Barista
}

/// <summary>Lifecycle of a receipt print job.</summary>
public enum PrintJobStatus
{
    /// <summary>Waiting to be picked up by the print agent.</summary>
    Pending,

    /// <summary>Fetched by the print agent, not acknowledged yet.</summary>
    Dispatched,

    /// <summary>Agent confirmed the printer accepted the copy.</summary>
    Printed,

    /// <summary>Agent gave up after retries; see LastError.</summary>
    Failed,

    /// <summary>Never printed on purpose (e.g. order cancelled, admin decision).</summary>
    Cancelled
}

/// <summary>
/// A queued receipt print produced when an order is paid — one row per copy
/// (customer + barista). The print agent polls the API for these.
/// </summary>
public class PrintJob : BaseAuditableEntity
{
    public Guid OrderId { get; private set; }

    public string OrderTrackingCode { get; private set; } = string.Empty;

    public PrintJobCopyType CopyType { get; private set; }

    public PrintJobStatus Status { get; private set; } = PrintJobStatus.Pending;

    /// <summary>
    /// Self-contained receipt payload (JSON) so the agent can render and print
    /// without any further API round-trips: header, items, totals, footer.
    /// </summary>
    public string PayloadJson { get; private set; } = string.Empty;

    /// <summary>How many times the agent attempted to send this to the printer.</summary>
    public int AttemptCount { get; private set; }

    public DateTimeOffset? DispatchedAt { get; private set; }

    public DateTimeOffset? PrintedAt { get; private set; }

    public string? LastError { get; private set; }

    private PrintJob()
    {
        // EF Core
    }

    public static PrintJob Create(
        Guid orderId,
        string orderTrackingCode,
        PrintJobCopyType copyType,
        string payloadJson)
    {
        var job = new PrintJob
        {
            OrderId = orderId,
            OrderTrackingCode = orderTrackingCode.Trim(),
            CopyType = copyType,
            Status = PrintJobStatus.Pending,
            PayloadJson = payloadJson
        };

        return job;
    }

    public void MarkDispatched(DateTimeOffset? nextDispatchedAt = null)
    {
        Status = PrintJobStatus.Dispatched;
        DispatchedAt = nextDispatchedAt ?? DateTimeOffset.UtcNow;
    }

    public void MarkBackToPending()
    {
        Status = PrintJobStatus.Pending;
        DispatchedAt = null;
    }

    public void MarkPrinted(DateTimeOffset? printedAt = null)
    {
        Status = PrintJobStatus.Printed;
        PrintedAt = printedAt ?? DateTimeOffset.UtcNow;
    }

    public void MarkFailed(string? error)
    {
        Status = PrintJobStatus.Failed;
        AttemptCount++;
        LastError = string.IsNullOrWhiteSpace(error) ? null : error.Trim();
    }

    /// <summary>Stale dispatches (agent died mid-print) go back to the queue.</summary>
    public bool IsStaleDispatch(TimeSpan retention)
        => Status == PrintJobStatus.Dispatched
            && DispatchedAt.HasValue
            && DateTimeOffset.UtcNow - DispatchedAt.Value > retention;
}
