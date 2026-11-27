using System.Text;

namespace Sugarista.Application.Affiliates.Services;

public sealed record AffiliateIdentitySnapshot(
    Guid? CustomerId,
    string? PhoneNumber,
    string? Email = null,
    string? NationalId = null,
    string? DeliveryAddress = null);

public sealed record SelfReferralCheckResult(
    bool IsSelfReferral,
    string? MatchedSignal,
    string? Reason)
{
    public static readonly SelfReferralCheckResult Clean = new(false, null, null);
}

/// <summary>
/// Pure anti-fraud evaluator for Phase 3 (Task 3.1):
/// - Self-referral detection (CustomerId, Phone, NationalId, DeliveryAddress, Email)
/// - Phone normalization across Persian/Arabic digits and +98/0098/09 prefixes
/// - Duplicate click 1-hour deduplication window
/// </summary>
public static class AffiliateAntiFraudEvaluator
{
    public static readonly TimeSpan DuplicateClickWindow = TimeSpan.FromHours(1);

    public static string NormalizeDigits(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var sb = new StringBuilder(input.Length);
        foreach (var ch in input)
        {
            if (ch is >= '۰' and <= '۹')
            {
                sb.Append((char)('0' + (ch - '۰')));
            }
            else if (ch is >= '٠' and <= '٩')
            {
                sb.Append((char)('0' + (ch - '٠')));
            }
            else
            {
                sb.Append(ch);
            }
        }

        return sb.ToString();
    }

    public static string? NormalizePhone(string? rawPhone)
    {
        if (string.IsNullOrWhiteSpace(rawPhone))
        {
            return null;
        }

        var ascii = NormalizeDigits(rawPhone);
        var digits = new StringBuilder(ascii.Length);
        foreach (var ch in ascii)
        {
            if (char.IsDigit(ch))
            {
                digits.Append(ch);
            }
        }

        var d = digits.ToString();
        if (d.Length == 0)
        {
            return null;
        }

        if (d.StartsWith("0098", StringComparison.Ordinal) && d.Length == 14)
        {
            d = "0" + d[4..];
        }
        else if (d.StartsWith("98", StringComparison.Ordinal) && d.Length == 12)
        {
            d = "0" + d[2..];
        }
        else if (d.StartsWith("9", StringComparison.Ordinal) && d.Length == 10)
        {
            d = "0" + d;
        }

        return d;
    }

    public static string? NormalizeText(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var ascii = NormalizeDigits(raw).Trim().ToLowerInvariant();
        var parts = ascii.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 0 ? null : string.Join(' ', parts);
    }

    public static SelfReferralCheckResult DetectSelfReferral(
        AffiliateIdentitySnapshot buyer,
        AffiliateIdentitySnapshot affiliateOwner)
    {
        if (buyer.CustomerId.HasValue &&
            affiliateOwner.CustomerId.HasValue &&
            buyer.CustomerId.Value != Guid.Empty &&
            buyer.CustomerId.Value == affiliateOwner.CustomerId.Value)
        {
            return new SelfReferralCheckResult(
                true,
                "CustomerId",
                "خودمعرفی (Self-Referral): شناسه خریدار با بازاریاب یکسان است.");
        }

        var buyerPhone = NormalizePhone(buyer.PhoneNumber);
        var ownerPhone = NormalizePhone(affiliateOwner.PhoneNumber);
        if (buyerPhone is not null && ownerPhone is not null &&
            string.Equals(buyerPhone, ownerPhone, StringComparison.Ordinal))
        {
            return new SelfReferralCheckResult(
                true,
                "PhoneNumber",
                "خودمعرفی (Self-Referral): شماره موبایل خریدار با بازاریاب یکسان است.");
        }

        var buyerNationalId = NormalizeText(buyer.NationalId);
        var ownerNationalId = NormalizeText(affiliateOwner.NationalId);
        if (buyerNationalId is not null && ownerNationalId is not null &&
            string.Equals(buyerNationalId, ownerNationalId, StringComparison.Ordinal))
        {
            return new SelfReferralCheckResult(
                true,
                "NationalId",
                "خودمعرفی (Self-Referral): کد ملی خریدار با بازاریاب یکسان است.");
        }

        var buyerAddress = NormalizeText(buyer.DeliveryAddress);
        var ownerAddress = NormalizeText(affiliateOwner.DeliveryAddress);
        if (buyerAddress is not null && ownerAddress is not null &&
            buyerAddress.Length >= 8 &&
            string.Equals(buyerAddress, ownerAddress, StringComparison.Ordinal))
        {
            return new SelfReferralCheckResult(
                true,
                "DeliveryAddress",
                "خودمعرفی (Self-Referral): آدرس تحویل خریدار با بازاریاب یکسان است.");
        }

        var buyerEmail = NormalizeText(buyer.Email);
        var ownerEmail = NormalizeText(affiliateOwner.Email);
        if (buyerEmail is not null && ownerEmail is not null &&
            string.Equals(buyerEmail, ownerEmail, StringComparison.Ordinal))
        {
            return new SelfReferralCheckResult(
                true,
                "Email",
                "خودمعرفی (Self-Referral): ایمیل خریدار با بازاریاب یکسان است.");
        }

        return SelfReferralCheckResult.Clean;
    }

    public static bool IsDuplicateClickWithinWindow(
        DateTimeOffset? lastClickCreatedAt,
        DateTimeOffset now,
        TimeSpan? window = null)
    {
        if (!lastClickCreatedAt.HasValue)
        {
            return false;
        }

        var effectiveWindow = window ?? DuplicateClickWindow;
        var elapsed = now - lastClickCreatedAt.Value;
        return elapsed >= TimeSpan.Zero && elapsed < effectiveWindow;
    }

    public static IReadOnlyList<string> BuildPhoneLookupVariants(string? rawPhone)
    {
        var canonical = NormalizePhone(rawPhone);
        if (canonical is null)
        {
            return [];
        }

        var variants = new HashSet<string>(StringComparer.Ordinal) { canonical };
        if (!string.IsNullOrWhiteSpace(rawPhone))
        {
            variants.Add(rawPhone.Trim());
        }

        if (canonical.StartsWith("09", StringComparison.Ordinal) && canonical.Length == 11)
        {
            variants.Add(canonical[1..]);       // 912...
            variants.Add("98" + canonical[1..]); // 98912...
            variants.Add("+98" + canonical[1..]); // +98912...
            variants.Add("0098" + canonical[1..]); // 0098912...
        }

        return variants.ToList();
    }
}
