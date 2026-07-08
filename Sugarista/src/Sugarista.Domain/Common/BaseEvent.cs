namespace Sugarista.Domain.Common;

public abstract class BaseEvent
{
    public DateTimeOffset OccurredOn { get; } = DateTimeOffset.UtcNow;
}
