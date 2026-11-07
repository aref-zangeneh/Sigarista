namespace Sugarista.Application.Pickup;

/// <summary>
/// Serializes capacity checks for one pickup slot. The returned handle keeps a
/// database transaction (and, on PostgreSQL, a transaction-scoped advisory
/// lock) open until <see cref="IPickupSlotReservation.CommitAsync"/> is called,
/// so two concurrent checkouts can never both consume the last place.
/// </summary>
public interface IPickupSlotReservationService
{
    Task<IPickupSlotReservation> ReserveAsync(
        DateTimeOffset slotStart,
        CancellationToken cancellationToken = default);
}

public interface IPickupSlotReservation : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);
}
