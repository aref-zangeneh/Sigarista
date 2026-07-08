using Sugarista.Domain.Common;

namespace Sugarista.Domain.UnitTests;

public class BaseEntityTests
{
    private sealed class TestEntity : BaseEntity
    {
    }

    private sealed class TestEvent : BaseEvent
    {
    }

    [Fact]
    public void AddDomainEvent_ShouldTrackEvents()
    {
        var entity = new TestEntity();
        var domainEvent = new TestEvent();

        entity.AddDomainEvent(domainEvent);

        Assert.Single(entity.DomainEvents);
        Assert.Contains(domainEvent, entity.DomainEvents);
    }

    [Fact]
    public void ClearDomainEvents_ShouldRemoveAllEvents()
    {
        var entity = new TestEntity();
        entity.AddDomainEvent(new TestEvent());

        entity.ClearDomainEvents();

        Assert.Empty(entity.DomainEvents);
    }
}
