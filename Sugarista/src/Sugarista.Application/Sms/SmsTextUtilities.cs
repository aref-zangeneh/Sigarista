using System.Globalization;
using System.Text;

namespace Sugarista.Application.Sms;

/// <summary>
/// Pure SMS text/number logic used by every send path:
///   • Iranian mobile normalization (09…, +98…, 0098…, 9… → 98912…).
///   • SMS segment counting (GSM-7: 160/153 chars, UCS-2: 70/67 chars).
///   • {Placeholder} rendering with sample or real values.
/// Unit-tested — these rules gate what can actually go out.
/// </summary>
public static class SmsTextUtilities
{
    /// <summary>Normalized form used everywhere: 98 + 10 digits (e.g. 989121234567).</summary>
    public static string? NormalizeMobile(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var digits = new string(input.Trim()
            .Replace('۰', '0')
            .Replace('۱', '1')
            .Replace('۲', '2')
            .Replace('۳', '3')
            .Replace('۴', '4')
            .Replace('۵', '5')
            .Replace('۶', '6')
            .Replace('۷', '7')
            .Replace('۸', '8')
            .Replace('۹', '9')
            .Where(char.IsDigit)
            .ToArray());

        var normalized = digits switch
        {
            { Length: 11 } when digits.StartsWith("09") => $"98{digits[1..]}",
            { Length: 12 } when digits.StartsWith("989") => digits,
            { Length: 14 } when digits.StartsWith("0098") => digits[2..],
            { Length: 10 } when digits.StartsWith("9") => $"98{digits}",
            _ => null
        };

        return normalized is { Length: 12 } && normalized.StartsWith("989") ? normalized : null;
    }

    /// <summary>Pretty local form for the UI: 0912….</summary>
    public static string ToDisplayMobile(string normalized) =>
        normalized.Length == 12 && normalized.StartsWith("98")
            ? $"0{normalized[2..]}"
            : normalized;

    /// <summary>Placeholder tokens the template engine supports.</summary>
    public static readonly IReadOnlyList<string> PlaceholderNames =
        ["FirstName", "LastName", "Mobile", "OrderNumber", "Amount", "Date", "TrackingCode"];

    public static IReadOnlyDictionary<string, string> SampleValues { get; } = new Dictionary<string, string>
    {
        ["FirstName"] = "علی",
        ["LastName"] = "رضایی",
        ["Mobile"] = "09121234567",
        ["OrderNumber"] = "1042",
        ["Amount"] = "285,000",
        ["Date"] = "۱۴۰۵/۰۸/۲۴",
        ["TrackingCode"] = "SG-8831"
    };

    /// <summary>
    /// Renders {Name} placeholders from the values; unknown placeholders are
    /// left as-is so a missing variable is VISIBLE in the preview instead of
    /// silently sending a broken message.
    /// </summary>
    public static string RenderTemplate(string template, IReadOnlyDictionary<string, string>? values)
    {
        if (string.IsNullOrEmpty(template) || !template.Contains('{'))
        {
            return template;
        }

        var builder = new StringBuilder(template.Length + 64);
        for (var index = 0; index < template.Length; index++)
        {
            if (template[index] != '{')
            {
                builder.Append(template[index]);
                continue;
            }

            var close = template.IndexOf('}', index);
            if (close < 0)
            {
                builder.Append(template[index..]);
                break;
            }

            var name = template[(index + 1)..close].Trim();
            if (values is not null && values.TryGetValue(name, out var value))
            {
                builder.Append(value);
            }
            else
            {
                builder.Append('{').Append(name).Append('}');
            }

            index = close;
        }

        return builder.ToString();
    }

    /// <summary>The placeholder names used by a template.</summary>
    public static IReadOnlyList<string> ExtractPlaceholders(string template)
    {
        var names = new List<string>();
        if (string.IsNullOrEmpty(template))
        {
            return names;
        }

        for (var index = 0; index < template.Length; index++)
        {
            if (template[index] != '{')
            {
                continue;
            }

            var close = template.IndexOf('}', index);
            if (close < 0)
            {
                break;
            }

            var name = template[(index + 1)..close].Trim();
            if (name.Length > 0 && !names.Contains(name))
            {
                names.Add(name);
            }

            index = close;
        }

        return names;
    }

    /// <summary>
    /// GSM-7 alphabet: the printable ASCII block plus the GSM-7 extension
    /// characters. Persian/Arabic text is NOT in GSM-7, so any Persian
    /// character correctly forces UCS-2 (70-char segments).
    /// </summary>
    private static bool IsGsm7(char character) =>
        character is >= ' ' and <= '~'                 // printable ASCII block
            or '\n' or '\r'                            // control chars in GSM-7
            or '£' or '¤' or '¥' or '§' or '¿'         // currency / symbols
            or 'Δ' or 'Φ' or 'Γ' or 'Λ' or 'Ω' or 'Π' or 'Ψ' or 'Σ' or 'Θ' or 'Ξ'
            or 'Æ' or 'æ' or 'ß' or 'É' or '¡' or 'Ä' or 'Ö' or 'Ñ' or 'Ü'
            or 'ä' or 'ö' or 'ñ' or 'ü' or 'à' or 'Ç' or 'è' or 'é' or 'ù' or 'ò'
            or 'Ø' or 'ø' or 'Å' or 'å';

    public static (int Segments, int Characters, bool Unicode) ComputeSegments(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return (0, 0, false);
        }

        var characters = text.Length;
        var unicode = text.Any(character => !IsGsm7(character));
        var (single, continuation) = unicode ? (70, 67) : (160, 153);

        return characters <= single
            ? (1, characters, unicode)
            : ((characters + continuation - 1) / continuation, characters, unicode);
    }
}
