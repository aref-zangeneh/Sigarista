using System.Text;

namespace Sugarista.Application.Common.Text;

/// <summary>
/// Helpers for converting Persian (۰-۹) and Arabic-Indic (٠-٩) digits
/// to canonical ASCII digits (0-9) at ingestion boundaries while keeping
/// sensitive secret/password fields completely untouched.
/// </summary>
public static class DigitNormalizer
{
    public const char ArabicDecimalSeparator = '\u066B'; // ٫
    public const char ArabicThousandsSeparator = '\u066C'; // ٬

    private static readonly string[] SensitiveSubstrings =
    [
        "password",
        "passwd",
        "secret",
        "apikey",
        "api_key",
        "signingkey",
        "signing_key",
        "privatekey",
        "private_key",
        "connectionstring",
        "connection_string",
        "accesstoken",
        "access_token",
        "refreshtoken",
        "refresh_token",
        "idtoken",
        "id_token",
        "bearertoken",
        "bearer_token",
        "extrasettingsjson",
    ];

    /// <summary>
    /// Fast check for Persian digits (۰-۹), Arabic-Indic digits (٠-٩),
    /// or the Arabic decimal separator (٫).
    /// </summary>
    public static bool ContainsNonAsciiDigits(ReadOnlySpan<char> value)
    {
        foreach (var ch in value)
        {
            if (IsPersianOrArabicDigit(ch) || ch == ArabicDecimalSeparator)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Converts Persian (۰-۹) and Arabic-Indic (٠-٩) digits inside <paramref name="value"/>
    /// to ASCII digits (0-9). Returns the exact original string when no conversion is needed.
    /// </summary>
    public static string? ToEnglishDigits(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        var hasConvertibleDigit = false;
        foreach (var ch in value)
        {
            if (IsPersianOrArabicDigit(ch))
            {
                hasConvertibleDigit = true;
                break;
            }
        }

        if (!hasConvertibleDigit)
        {
            return value;
        }

        return string.Create(value.Length, value, static (span, source) =>
        {
            for (var i = 0; i < source.Length; i++)
            {
                span[i] = MapDigit(source[i]);
            }
        });
    }

    /// <summary>
    /// Normalizes a numeric string typed by users in Persian/Arabic/English layout
    /// into a plain invariant numeric token ready for parsing.
    /// </summary>
    public static string NormalizeNumericToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        var builder = new StringBuilder(trimmed.Length);

        foreach (var ch in trimmed)
        {
            if (ch is ',' or ArabicThousandsSeparator or '\u060C' or '\u00A0' or '\u200C' || char.IsWhiteSpace(ch))
            {
                continue;
            }

            if (ch == ArabicDecimalSeparator) // ٫
            {
                builder.Append('.');
                continue;
            }

            if (ch == '\u2212') // Unicode minus sign −
            {
                builder.Append('-');
                continue;
            }

            builder.Append(MapDigit(ch));
        }

        return builder.ToString();
    }

    /// <summary>
    /// Returns true when a field, property, or query parameter name represents a secret,
    /// password, credential, or raw provider JSON whose characters must never be altered.
    /// </summary>
    public static bool IsSensitiveFieldName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        var trimmed = name.Trim();
        if (trimmed.Equals("token", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("hash", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("signature", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        foreach (var substring in SensitiveSubstrings)
        {
            if (trimmed.Contains(substring, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsPersianOrArabicDigit(char ch) =>
        ch is (>= '\u06F0' and <= '\u06F9') or (>= '\u0660' and <= '\u0669');

    private static char MapDigit(char ch) => ch switch
    {
        >= '\u06F0' and <= '\u06F9' => (char)('0' + (ch - '\u06F0')),
        >= '\u0660' and <= '\u0669' => (char)('0' + (ch - '\u0660')),
        _ => ch,
    };
}
