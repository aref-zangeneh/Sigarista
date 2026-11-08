using System.Security.Cryptography;
using System.Text;
using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

/// <summary>
/// A persistent phone-login session of the digital menu: the customer signs in
/// with only their phone number and stays signed in on that device for the
/// duration configured in site settings. Only a SHA-256 hash of the session
/// token is stored — the raw token lives solely in the customer's cookie, so a
/// database leak cannot mint sessions.
/// </summary>
public class CustomerSession : BaseEntity
{
    private CustomerSession()
    {
    }

    public CustomerSession(Guid customerId, string token, DateTimeOffset nowUtc, int lifetimeDays)
    {
        if (customerId == Guid.Empty)
        {
            throw new DomainException("Customer id is required for a session.");
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            throw new DomainException("Session token is required.");
        }

        if (lifetimeDays < 1)
        {
            throw new DomainException("Session lifetime must be at least one day.");
        }

        CustomerId = customerId;
        TokenHash = HashToken(token);
        ExpiresAtUtc = nowUtc.AddDays(lifetimeDays);
        LastSeenAtUtc = nowUtc;
    }

    public Guid CustomerId { get; private set; }

    /// <summary>SHA-256 hex of the session token carried by the customer's cookie.</summary>
    public string TokenHash { get; private set; } = string.Empty;

    public DateTimeOffset ExpiresAtUtc { get; private set; }

    public DateTimeOffset LastSeenAtUtc { get; private set; }

    public DateTimeOffset? RevokedAtUtc { get; private set; }

    public Customer Customer { get; private set; } = null!;

    public bool IsActiveAt(DateTimeOffset nowUtc) => RevokedAtUtc is null && ExpiresAtUtc > nowUtc;

    /// <summary>
    /// Records activity on the session. Throttled to at most once per hour so a
    /// busy menu refreshes the column without any extra write pressure.
    /// </summary>
    public void Touch(DateTimeOffset nowUtc)
    {
        if (nowUtc - LastSeenAtUtc >= TimeSpan.FromHours(1))
        {
            LastSeenAtUtc = nowUtc;
        }
    }

    public void Revoke(DateTimeOffset nowUtc) => RevokedAtUtc ??= nowUtc;

    public static string NewToken()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    public static string HashToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new DomainException("Session token is required.");
        }

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }
}
