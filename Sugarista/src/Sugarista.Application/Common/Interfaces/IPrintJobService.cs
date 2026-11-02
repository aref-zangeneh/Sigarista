using Sugarista.Domain.Entities;

namespace Sugarista.Application.Common.Interfaces;

/// <summary>
/// Queues thermal-receipt print jobs for a paid order (customer copy +
/// barista copy). A print agent running on the store's laptop picks these
/// up through the API and sends them to the local receipt printer.
/// </summary>
public interface IPrintJobService
{
    /// <summary>
    /// Enqueues both receipt copies (customer + barista) for the order.
    /// Idempotent: no new jobs are created when the order already has
    /// any active (non-cancelled) print jobs.
    /// </summary>
    Task EnqueueOrderReceiptsAsync(Order order, CancellationToken cancellationToken);
}
