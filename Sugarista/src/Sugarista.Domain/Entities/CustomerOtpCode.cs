using System.Security.Cryptography;
using System.Text;
using Sugarista.Domain.Common;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.Entities;

/// <summary>
/// One-time SMS verification code for customer authentication on the digital menu.
/// Stores only a salted SHA-256 hash of the code (never plaintext), enforces expiry,
/// attempt limits, and single-use consumption.
/// </summary>
public class CustomerOtpCode : BaseEntity
{
    public const int DefaultCodeLength = 6;
    public const int DefaultExpirySeconds = 120;
    public const int DefaultCooldownSeconds = 60;
    public const int DefaultMaxAttempts = 5;

    private CustomerOtpCode()
    {
    }

    public CustomerOtpCode(
        string phoneNumber,
        string code,
        DateTimeOffset nowUtc,
        int expirySeconds = DefaultExpirySeconds,
        int maxAttempts = DefaultMaxAttempts,
        string? requestIp = null)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            throw new DomainException("Phone number is required for an OTP code.");
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            throw new DomainException("OTP code is required.");
        }

        if (expirySeconds < 30 || expirySeconds > 900)
        {
            throw new DomainException("OTP expiry must be between 30 and 900 seconds.");
        }

        if (maxAttempts < 1 || maxAttempts > 10)
        {
            throw new DomainException("OTP max attempts must be between 1 and 10.");
        }

        PhoneNumber = phoneNumber.Trim();
        Salt = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        CodeHash = ComputeHash(PhoneNumber, code.Trim(), Salt);
        CreatedAtUtc = nowUtc;
        ExpiresAtUtc = nowUtc.AddSeconds(expirySeconds);
        MaxAttempts = maxAttempts;
        AttemptCount = 0;
        RequestIp = string.IsNullOrWhiteSpace(requestIp) ? null : requestIp.Trim()[..Math.Min(requestIp.Trim().Length, 64)];
    }

    public string PhoneNumber { get; private set; } = string.Empty;

    public string CodeHash { get; private set; } = string.Empty;

    public string Salt { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset ExpiresAtUtc { get; private set; }

    public DateTimeOffset? ConsumedAtUtc { get; private set; }

    public int AttemptCount { get; private set; }

    public int MaxAttempts { get; private set; } = DefaultMaxAttempts;

    public string? RequestIp { get; private set; }

    public bool IsConsumed => ConsumedAtUtc.HasValue;

    public bool IsExhausted => AttemptCount >= MaxAttempts;

    public bool IsExpiredAt(DateTimeOffset nowUtc) => ExpiresAtUtc <= nowUtc;

    public bool CanVerifyAt(DateTimeOffset nowUtc) => !IsConsumed && !IsExhausted && !IsExpiredAt(nowUtc);

    public bool VerifyAndConsume(string candidateCode, DateTimeOffset nowUtc)
    {
        if (!CanVerifyAt(nowUtc))
        {
            return false;
        }

        var normalizedCandidate = (candidateCode ?? string.Empty).Trim();
        if (normalizedCandidate.Length == 0)
        {
            AttemptCount++;
            return false;
        }

        var expectedBytes = Encoding.UTF8.GetBytes(CodeHash);
        var actualBytes = Encoding.UTF8.GetBytes(ComputeHash(PhoneNumber, normalizedCandidate, Salt));
        var matched = expectedBytes.Length == actualBytes.Length
                      && CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);

        if (matched)
        {
            ConsumedAtUtc = nowUtc;
            return true;
        }

        AttemptCount++;
        return false;
    }

    public void Invalidate(DateTimeOffset nowUtc)
    {
        ConsumedAtUtc ??= nowUtc;
    }

    public static string GenerateNumericCode(int digits = DefaultCodeLength)
    {
        if (digits is < 4 or > 8)
        {
            throw new DomainException("OTP code length must be between 4 and 8 digits.");
        }

        var min = (int)Math.Pow(10, digits - 1);
        var maxExclusive = (int)Math.Pow(10, digits);
        return RandomNumberGenerator.GetInt32(min, maxExclusive).ToString();
    }

    public static string ComputeHash(string phoneNumber, string code, string salt)
    {
        var input = $"{phoneNumber.Trim()}:{code.Trim()}:{salt.Trim()}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input)));
    }
}
