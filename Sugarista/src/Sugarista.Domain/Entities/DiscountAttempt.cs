using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

/// <summary>
/// One attempt to use a discount code, recorded to detect abuse (brute-forcing
/// codes). Failures are counted inside the lockout window configured in
/// site settings; a successful use clears the counter.
/// </summary>
public class DiscountAttempt : BaseEntity
{
    private DiscountAttempt()
    {
    }

    public DiscountAttempt(
        string identifier,
        string? code,
        bool succeeded,
        string? ipAddress = null,
        DateTimeOffset? attemptedAt = null)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            throw new DomainException("A discount attempt needs an identifier.");
        }

        Identifier = identifier.Trim();
        Code = Discount.NormalizeCode(code);
        Succeeded = succeeded;
        IpAddress = string.IsNullOrWhiteSpace(ipAddress) ? null : ipAddress.Trim();
        AttemptedAt = attemptedAt ?? DateTimeOffset.UtcNow;
    }

    /// <summary>Normalized phone number (or user id) the attempt belongs to.</summary>
    public string Identifier { get; private set; } = string.Empty;

    /// <summary>The normalized code that was typed (null for automatic discounts).</summary>
    public string? Code { get; private set; }

    public bool Succeeded { get; private set; }

    public string? IpAddress { get; private set; }

    public DateTimeOffset AttemptedAt { get; private set; }
}
