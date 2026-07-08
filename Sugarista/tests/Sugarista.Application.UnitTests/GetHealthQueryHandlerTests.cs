using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.Health.Queries.GetHealth;

namespace Sugarista.Application.UnitTests;

public class GetHealthQueryHandlerTests
{
    [Fact]
    public async Task Handle_ShouldReturnHealthyStatus()
    {
        var handler = new GetHealthQueryHandler(new FixedDateTime(new DateTimeOffset(2026, 7, 5, 0, 0, 0, TimeSpan.Zero)));

        var result = await handler.Handle(new GetHealthQuery(), CancellationToken.None);

        Assert.Equal("Healthy", result.Status);
        Assert.Equal(new DateTimeOffset(2026, 7, 5, 0, 0, 0, TimeSpan.Zero), result.Timestamp);
        Assert.False(string.IsNullOrWhiteSpace(result.Version));
    }

    private sealed class FixedDateTime(DateTimeOffset utcNow) : IDateTime
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }
}
