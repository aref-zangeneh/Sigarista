using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Sugarista.Application.Pickup;
using Sugarista.Infrastructure.Data;

namespace Sugarista.Infrastructure.Services;

/// <summary>
/// PostgreSQL implementation of the pickup capacity lock. A transaction-scoped
/// advisory lock keyed by the slot start serializes concurrent checkouts for
/// the same slot; the transaction is only committed after the order row exists.
/// </summary>
public sealed class PickupSlotReservationService : IPickupSlotReservationService
{
    private readonly ApplicationDbContext _dbContext;

    public PickupSlotReservationService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IPickupSlotReservation> ReserveAsync(
        DateTimeOffset slotStart,
        CancellationToken cancellationToken = default)
    {
        if (!_dbContext.Database.IsRelational())
        {
            return NoopPickupSlotReservation.Instance;
        }

        var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            // One lock per slot start. hashtext() keeps the key stable and
            // short; pg_advisory_xact_lock is released automatically on
            // commit/rollback, so a crashed request never blocks the slot.
            var key = slotStart.UtcDateTime.ToString("yyyyMMddHHmm");
            await _dbContext.Database.ExecuteSqlRawAsync(
                "SELECT pg_advisory_xact_lock(hashtext({0}))",
                [key],
                cancellationToken);

            return new TransactionPickupSlotReservation(transaction);
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }

    private sealed class TransactionPickupSlotReservation : IPickupSlotReservation
    {
        private readonly IDbContextTransaction _transaction;
        private bool _committed;

        public TransactionPickupSlotReservation(IDbContextTransaction transaction)
        {
            _transaction = transaction;
        }

        public async Task CommitAsync(CancellationToken cancellationToken = default)
        {
            await _transaction.CommitAsync(cancellationToken);
            _committed = true;
        }

        public async ValueTask DisposeAsync()
        {
            if (!_committed)
            {
                await _transaction.RollbackAsync();
            }

            await _transaction.DisposeAsync();
        }
    }

    private sealed class NoopPickupSlotReservation : IPickupSlotReservation
    {
        public static readonly NoopPickupSlotReservation Instance = new();

        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
