namespace Sugarista.Application.Health.Queries.GetHealth;

public record GetHealthQuery : IRequest<HealthDto>;

public record HealthDto(string Status, DateTimeOffset Timestamp, string Version);

public class GetHealthQueryHandler : IRequestHandler<GetHealthQuery, HealthDto>
{
    private readonly IDateTime _dateTime;

    public GetHealthQueryHandler(IDateTime dateTime)
    {
        _dateTime = dateTime;
    }

    public Task<HealthDto> Handle(GetHealthQuery request, CancellationToken cancellationToken)
    {
        var version = typeof(GetHealthQueryHandler).Assembly
            .GetName()
            .Version?
            .ToString() ?? "1.0.0";

        return Task.FromResult(new HealthDto("Healthy", _dateTime.UtcNow, version));
    }
}
