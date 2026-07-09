using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

public class SalonTable : BaseAuditableEntity
{
    private readonly List<TableSession> _sessions = [];

    public SalonTable()
    {
    }

    public SalonTable(string name, int capacity, int displayOrder, bool isActive = true)
    {
        Update(name, capacity, displayOrder, isActive);
    }

    public string Name { get; private set; } = string.Empty;

    public int Capacity { get; private set; }

    public int DisplayOrder { get; private set; }

    public bool IsActive { get; private set; } = true;

    public IReadOnlyCollection<TableSession> Sessions => _sessions.AsReadOnly();

    public void Update(string name, int capacity, int displayOrder, bool isActive)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Table name is required.");
        }

        if (capacity <= 0)
        {
            throw new DomainException("Table capacity must be greater than zero.");
        }

        Name = name.Trim();
        Capacity = capacity;
        DisplayOrder = displayOrder;
        IsActive = isActive;
    }
}
