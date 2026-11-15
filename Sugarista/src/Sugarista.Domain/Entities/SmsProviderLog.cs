using Sugarista.Domain.Common;

namespace Sugarista.Domain.Entities;

/// <summary>
/// Provider interaction log (send attempts, connection tests, credit
/// checks) — cheap operational trace next to the message history.
/// </summary>
public class SmsProviderLog : BaseEntity
{
    public SmsProviderLog(Guid? providerId, string action, bool success, string? detail)
    {
        ProviderId = providerId;
        Action = action;
        Success = success;
        Detail = detail is { Length: > 500 } ? detail[..500] : detail;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    private SmsProviderLog()
    {
    }

    public Guid? ProviderId { get; private set; }

    public string Action { get; private set; } = string.Empty;

    public bool Success { get; private set; }

    public string? Detail { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}
