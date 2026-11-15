using Sugarista.Domain.Common;

namespace Sugarista.Domain.Entities;

/// <summary>
/// A number that opted out (or was blocked) from messages — especially
/// marketing. Bulk sends must exclude these; the panel can add/remove.
/// </summary>
public class SmsOptOut : BaseEntity
{
    public SmsOptOut(string mobile, string? reason)
    {
        Mobile = mobile;
        Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
    }

    private SmsOptOut()
    {
    }

    /// <summary>Normalized mobile (98…), unique.</summary>
    public string Mobile { get; private set; } = string.Empty;

    public string? Reason { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
}
