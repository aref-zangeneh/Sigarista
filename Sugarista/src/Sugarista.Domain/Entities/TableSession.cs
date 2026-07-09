using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

public class TableSession : BaseAuditableEntity
{
    public TableSession()
    {
    }

    public TableSession(Guid tableId, int peopleCount, DateTimeOffset startedAt, string? note)
    {
        if (tableId == Guid.Empty)
        {
            throw new DomainException("Table is required.");
        }

        if (peopleCount <= 0)
        {
            throw new DomainException("People count must be greater than zero.");
        }

        TableId = tableId;
        PeopleCount = peopleCount;
        StartedAt = startedAt;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }

    public Guid TableId { get; private set; }

    public SalonTable Table { get; private set; } = null!;

    public int PeopleCount { get; private set; }

    public DateTimeOffset StartedAt { get; private set; }

    public DateTimeOffset? EndedAt { get; private set; }

    public string? Note { get; private set; }

    public bool IsActive => EndedAt is null;

    public void Release(DateTimeOffset endedAt)
    {
        if (endedAt < StartedAt)
        {
            throw new DomainException("Session end time cannot be before start time.");
        }

        EndedAt = endedAt;
    }
}
