using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Domain.Entities;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Application.Discounts.Security;

/// <summary>
/// Guards discount codes against abuse: a blacklist of phone numbers/IPs, a
/// temporary lock after too many failed attempts, and a successful-use reset.
/// All thresholds come from site settings, so the admin can change them live.
/// </summary>
public static class DiscountAbuseGuard
{
    /// <summary>Normalizes a phone/user identifier to digits (empty when absent).</summary>
    public static string NormalizeIdentifier(string? value) =>
        new string((value ?? string.Empty).Where(char.IsDigit).ToArray());

    /// <summary>
    /// Throws a Persian <see cref="DomainException"/> when the phone/IP is
    /// blacklisted or currently locked after too many failed code attempts.
    /// </summary>
    public static async Task EnsureAllowedAsync(
        IApplicationDbContext dbContext,
        Sugarista.Domain.Entities.SiteSettings? settings,
        string? phoneNumber,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        if (settings is null || !settings.DiscountAbuseProtectionEnabled)
        {
            return;
        }

        var identifier = NormalizeIdentifier(phoneNumber);
        var ip = NormalizeIp(ipAddress);

        if (await IsBlacklistedAsync(dbContext, identifier, ip, cancellationToken))
        {
            throw new DomainException(
                "امکان استفاده از تخفیف برای این شماره یا کاربر مسدود شده است. برای پیگیری با پشتیبانی تماس بگیرید.");
        }

        if (identifier.Length == 0)
        {
            return;
        }

        var lockoutMinutes = Math.Max(1, settings.DiscountLockoutMinutes);
        var maxFailures = Math.Max(1, settings.DiscountMaxFailedAttempts);
        var now = DateTimeOffset.UtcNow;
        var windowStart = now.AddMinutes(-lockoutMinutes);

        var lastSuccess = await dbContext.DiscountAttempts
            .AsNoTracking()
            .Where(attempt => attempt.Identifier == identifier && attempt.Succeeded)
            .OrderByDescending(attempt => attempt.AttemptedAt)
            .Select(attempt => (DateTimeOffset?)attempt.AttemptedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (lastSuccess is { } successAt && successAt > windowStart)
        {
            windowStart = successAt;
        }

        var failures = await dbContext.DiscountAttempts
            .AsNoTracking()
            .Where(attempt => attempt.Identifier == identifier &&
                              !attempt.Succeeded &&
                              attempt.AttemptedAt > windowStart)
            .ToListAsync(cancellationToken);

        if (failures.Count < maxFailures)
        {
            return;
        }

        var lastFailure = failures.Max(attempt => attempt.AttemptedAt);
        var unlockAt = lastFailure.AddMinutes(lockoutMinutes);
        var remainingMinutes = Math.Max(1, (int)Math.Ceiling((unlockAt - now).TotalMinutes));
        throw new DomainException(
            $"تعداد تلاش‌های ناموفق برای کد تخفیف زیاد است. لطفاً {remainingMinutes} دقیقهٔ دیگر دوباره تلاش کنید.");
    }

    /// <summary>Records a failed code attempt (no-op when protection is disabled).</summary>
    public static async Task RecordFailureAsync(
        IApplicationDbContext dbContext,
        Sugarista.Domain.Entities.SiteSettings? settings,
        string? phoneNumber,
        string? code,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        if (settings is null || !settings.DiscountAbuseProtectionEnabled)
        {
            return;
        }

        var identifier = NormalizeIdentifier(phoneNumber);
        if (identifier.Length == 0)
        {
            identifier = NormalizeIp(ipAddress) ?? "unknown";
        }

        dbContext.DiscountAttempts.Add(new DiscountAttempt(
            identifier,
            code,
            succeeded: false,
            NormalizeIp(ipAddress)));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Records a successful use so previous failures stop counting.</summary>
    public static async Task RecordSuccessAsync(
        IApplicationDbContext dbContext,
        Sugarista.Domain.Entities.SiteSettings? settings,
        string? phoneNumber,
        string? code,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        if (settings is null || !settings.DiscountAbuseProtectionEnabled)
        {
            return;
        }

        var identifier = NormalizeIdentifier(phoneNumber);
        if (identifier.Length == 0)
        {
            return;
        }

        dbContext.DiscountAttempts.Add(new DiscountAttempt(
            identifier,
            code,
            succeeded: true,
            NormalizeIp(ipAddress)));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public static async Task<bool> IsBlacklistedAsync(
        IApplicationDbContext dbContext,
        string? identifier,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var values = new List<string>();
        if (!string.IsNullOrWhiteSpace(identifier))
        {
            values.Add(identifier);
        }

        if (!string.IsNullOrWhiteSpace(ipAddress))
        {
            values.Add(ipAddress);
        }

        if (values.Count == 0)
        {
            return false;
        }

        return await dbContext.BlacklistEntries
            .AsNoTracking()
            .AnyAsync(entry => entry.IsActive && values.Contains(entry.Value), cancellationToken);
    }

    private static string? NormalizeIp(string? ipAddress) =>
        string.IsNullOrWhiteSpace(ipAddress) ? null : ipAddress.Trim().ToLowerInvariant();
}
