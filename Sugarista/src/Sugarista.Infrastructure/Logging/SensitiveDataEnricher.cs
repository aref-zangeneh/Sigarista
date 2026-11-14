using Serilog.Core;
using Serilog.Events;

namespace Sugarista.Infrastructure.Logging;

/// <summary>
/// Defense-in-depth against secrets in logs: any property whose NAME matches
/// a sensitive pattern (password, token, authorization header, API keys,
/// connection strings, …) is replaced with a redacted marker before the
/// event reaches any sink. Case-insensitive substring matching on purpose —
/// <c>AdminPassword</c>, <c>accessToken</c>, <c>DbSecret</c> all match.
/// </summary>
public sealed class SensitiveDataEnricher : ILogEventEnricher
{
    private static readonly string[] SensitiveFragments =
    [
        "password", "passwd", "secret", "token", "bearer", "authorization",
        "apikey", "api-key", "api_key", "credential", "connectionstring",
        "creditcard", "credit-card", "cvv", "otp", "privatekey", "private-key"
    ];

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        foreach (var key in logEvent.Properties.Keys.ToList())
        {
            if (IsSensitive(key))
            {
                logEvent.AddOrUpdateProperty(
                    propertyFactory.CreateProperty(key, "«redacted»"));
            }
        }
    }

    private static bool IsSensitive(string propertyName)
    {
        foreach (var fragment in SensitiveFragments)
        {
            if (propertyName.Contains(fragment, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
