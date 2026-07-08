using Sugarista.Application.Common.Interfaces;

namespace Sugarista.Infrastructure.Services;

public class DateTimeService : IDateTime
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
